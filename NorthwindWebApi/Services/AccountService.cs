using AutoMapper;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using NorthwindWebApi.Entities;
using NorthwindWebApi.Helpers;
using NorthwindWebApi.Models.Accounts;
using NorthwindWebApi.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace NorthwindWebApi.Services
{
    public interface IAccountService
    {
        Task<AuthenticateResponse> AuthenticateAsync(AuthenticateRequest model);
        AuthenticateResponse RefreshToken(string token);
        void RevokeToken(string token);
        void Register(RegisterRequest model, string origin);
        void ValidateResetToken(ValidateResetTokenRequest model);
        IEnumerable<AccountResponse> GetAll();
        AccountResponse GetById(int id);
        AccountResponse Create(CreateRequest model);
        AccountResponse Update(int id, UpdateRequest model);
        void Delete(int id);
    }

    public class AccountService : IAccountService
    {
        private readonly NorthwindContext northwindContext;
        private readonly IdentityContext identityContext;
        private readonly IMapper _mapper;
        private readonly IConfiguration _configuration;
        private readonly UserManager<Account> _userManager;

        public AccountService(NorthwindContext nContext, IdentityContext iContext, IMapper mapper, IConfiguration configuration, UserManager<Account> userManager)
        {
            northwindContext = nContext;
            identityContext = iContext;
            _mapper = mapper;
            _configuration = configuration;
            _userManager = userManager;
        }

        public async Task<AuthenticateResponse> AuthenticateAsync(AuthenticateRequest model)
        {
            var account = identityContext.Accounts.SingleOrDefault(x => x.UserName == model.UserName);
            bool validPass = await _userManager.CheckPasswordAsync(account, model.Password);

            if (account == null || !validPass)
                throw new AppException("Username or password is incorrect");

            var refreshToken = account.RefreshTokens.LastOrDefault();
            if (!refreshToken.IsActive || refreshToken == null)
                refreshToken = generateRefreshToken();

            // authentication successful so generate jwt token
            var jwtToken = generateJwtToken(account);


            // save refresh token
            account.RefreshTokens.Add(refreshToken);
            identityContext.Update(account);
            identityContext.SaveChanges();

            var response = _mapper.Map<AuthenticateResponse>(account);
            response.JwtToken = jwtToken;
            response.RefreshToken = refreshToken.Token;
            return response;
        }

        public AuthenticateResponse RefreshToken(string token)
        {
            var (refreshToken, account) = getRefreshToken(token);

            // replace old refresh token with a new one and save
            var newRefreshToken = generateRefreshToken();
            refreshToken.Revoked = DateTime.UtcNow;
            refreshToken.ReplacedByToken = newRefreshToken.Token;
            account.RefreshTokens.Add(newRefreshToken);
            identityContext.Update(account);
            identityContext.SaveChanges();

            // generate new jwt
            var jwtToken = generateJwtToken(account);

            var response = _mapper.Map<AuthenticateResponse>(account);
            response.JwtToken = jwtToken;
            response.RefreshToken = newRefreshToken.Token;
            return response;
        }

        public void RevokeToken(string token)
        {
            var (refreshToken, account) = getRefreshToken(token);

            // revoke token and save
            refreshToken.Revoked = DateTime.UtcNow;
            identityContext.Update(account);
            identityContext.SaveChanges();
        }

        public void Register(RegisterRequest model, string origin)
        {
            // validate
            if (identityContext.Accounts.Any(x => x.Email == model.Email))
            {
                // send already registered error in email to prevent account enumeration
                throw new Exception("Account already exists");
                //return;
            }

            // map model to new account object
            var account = _mapper.Map<Account>(model);

            // first registered account is an admin
            var isFirstAccount = identityContext.Accounts.Count() == 0;
            account.Role = isFirstAccount ? Roles.Admin : Roles.Employee;
            account.Created = DateTime.UtcNow;
            account.JwtToken = randomTokenString();

            // hash password
            account.PasswordHash = model.Password;

            // save account
            identityContext.Accounts.Add(account);
            identityContext.SaveChanges();

        }

        public void ValidateResetToken(ValidateResetTokenRequest model)
        {
            var account = identityContext.Accounts.SingleOrDefault(x =>
                x.ResetToken == model.Token &&
                x.ResetTokenExpires > DateTime.UtcNow);

            if (account == null)
                throw new AppException("Invalid token");
        }

        public IEnumerable<AccountResponse> GetAll()
        {
            var accounts = identityContext.Accounts.ToList();
            return _mapper.Map<IList<AccountResponse>>(accounts);
        }

        public AccountResponse GetById(int id)
        {
            var account = getAccount(id);
            return _mapper.Map<AccountResponse>(account);
        }

        public AccountResponse Create(CreateRequest model)
        {
            // validate
            if (identityContext.Accounts.Any(x => x.Email == model.Email))
                throw new AppException($"Email '{model.Email}' is already registered");

            // map model to new account object
            var account = _mapper.Map<Account>(model);
            account.Created = DateTime.UtcNow;
            account.Verified = DateTime.UtcNow;

            // hash password
            account.PasswordHash = model.Password;

            // save account
            identityContext.Accounts.Add(account);
            identityContext.SaveChanges();

            return _mapper.Map<AccountResponse>(account);
        }

        public AccountResponse Update(int id, UpdateRequest model)
        {
            var account = getAccount(id);

            // validate
            if (account.Email != model.Email && identityContext.Accounts.Any(x => x.Email == model.Email))
                throw new AppException($"Email '{model.Email}' is already taken");

            // hash password if it was entered
            if (!string.IsNullOrEmpty(model.Password))
                account.PasswordHash = model.Password;

            // copy model to account and save
            _mapper.Map(model, account);
            account.Updated = DateTime.UtcNow;
            identityContext.Accounts.Update(account);
            identityContext.SaveChanges();

            return _mapper.Map<AccountResponse>(account);
        }

        public void Delete(int id)
        {
            var account = getAccount(id);
            identityContext.Accounts.Remove(account);
            identityContext.SaveChanges();
        }

        // helper methods

        private Account getAccount(int id)
        {
            var account = identityContext.Accounts.Find(id);
            if (account == null) throw new KeyNotFoundException("Account not found");
            return account;
        }

        private (RefreshToken, Account) getRefreshToken(string token)
        {
            var account = identityContext.Accounts.SingleOrDefault(u => u.RefreshTokens.Any(t => t.Token == token));
            if (account == null) throw new AppException("Invalid token");
            var refreshToken = account.RefreshTokens.Single(x => x.Token == token);
            if (!refreshToken.IsActive) throw new AppException("Invalid token");
            return (refreshToken, account);
        }

        private string generateJwtToken(Account account)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim("id", account.Id.ToString()) }),
                Expires = DateTime.UtcNow.AddMinutes(30),
                SigningCredentials = new SigningCredentials(key , SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private RefreshToken generateRefreshToken()
        {
            return new RefreshToken
            {
                Token = randomTokenString(),
                Expires = DateTime.UtcNow.AddDays(1),
                Created = DateTime.UtcNow,
            };
        }

        private string randomTokenString()
        {
            using var rngCryptoServiceProvider = new RNGCryptoServiceProvider();
            var randomBytes = new byte[40];
            rngCryptoServiceProvider.GetBytes(randomBytes);
            // convert random bytes to hex string
            return BitConverter.ToString(randomBytes).Replace("-", "");
        }
    }
}
