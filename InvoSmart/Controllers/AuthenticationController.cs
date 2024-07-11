using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Data;
using System.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;

namespace InvoSmart.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        public IConfiguration _configuration;

        public AuthenticationController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /**
         * LOGIN
         * 
         */
        [HttpPost]
        [Route("LoginClient")]
        public JsonResult LoginClient([FromForm] string clientEmail, [FromForm] string clientPassword)
        {
            string query = "SELECT clientID, clientFirstName, clientLastName, clientCompanyName, clientCompanyAddress, clientCompanyCity, clientCompanyState, clientCompanyZipCode, clientPhoneNumber, clientEmail, clientPassword FROM dbo.clients WHERE clientEmail = @clientEmail";
            DataTable table = new DataTable();
            string sqlDatasource = _configuration.GetConnectionString("invosmartDBCon");
            SqlDataReader myReader;

            using (SqlConnection myCon = new SqlConnection(sqlDatasource))
            {
                myCon.Open();
                using (SqlCommand myCommand = new SqlCommand(query, myCon))
                {
                    myCommand.Parameters.AddWithValue("@clientEmail", clientEmail.ToLower());
                    myReader = myCommand.ExecuteReader();
                    table.Load(myReader);
                    myReader.Close();
                    myCon.Close();
                }
            }

            if (table.Rows.Count == 1)
            {
                Console.WriteLine("Email validated");

                DataRow row = table.Rows[0];
                string hashedPasswordFromDB = row["clientPassword"].ToString();

                if (BCrypt.Net.BCrypt.Verify(clientPassword, hashedPasswordFromDB))
                {
                    Console.WriteLine("Password validated");

                    var token = GenerateJwtToken(clientEmail);
                    var userInfo = new
                    {
                        ClientID = row["clientID"].ToString(),
                        Email = row["clientEmail"].ToString().ToUpper(),
                        FirstName = row["clientFirstName"].ToString().ToUpper(),
                        LastName = row["clientLastName"].ToString().ToUpper(),
                        Company = row["clientCompanyName"].ToString().ToUpper(),
                        CompanyAddress = row["clientCompanyAddress"].ToString(),
                        CompanyCity = row["clientCompanyCity"].ToString().ToUpper(),
                        CompanyState = row["clientCompanyState"].ToString().ToUpper(),
                        CompanyZipCode = row["clientCompanyZipCode"].ToString(),
                        CompanyPhone = row["clientPhoneNumber"].ToString(),
                        Token = token
                    };
                    return new JsonResult(userInfo);
                }
            }

            Console.WriteLine("Incorrect Email or Password");
            return new JsonResult("Fail");
        }

        /**
         * SIGN-UP FUNCTION
         * 
         */
        [HttpPost]
        [Route("SignupClient")]
        public JsonResult SignupClient([FromForm] string newClientFName, [FromForm] string newClientLName, [FromForm] string newClientCompanyName, [FromForm] string newClientCompanyAddress, [FromForm] string newClientCompanyCity, [FromForm] string newClientCompanyState, [FromForm] string newClientCompanyZipCode, [FromForm] string newClientPhoneNumber, [FromForm] string newClientEmail, [FromForm] string newClientPassword)
        {
            int newClientID;
            DataTable table = new DataTable();
            string sqlDatasource = _configuration.GetConnectionString("invosmartDBCon");

            // Hash the password
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newClientPassword);

            SqlDataReader myReader;

            // Check for existing user by email or business
            using (SqlConnection myCon = new SqlConnection(sqlDatasource))
            {
                myCon.Open();

                // Check for existing user by email and business
                string checkQuery = "SELECT COUNT(1) FROM dbo.clients WHERE clientEmail = @newClientEmail OR clientCompanyName = @newClientCompanyName";
                using (SqlCommand checkCommand = new SqlCommand(checkQuery, myCon))
                {
                    checkCommand.Parameters.AddWithValue("@newClientEmail", newClientEmail);
                    checkCommand.Parameters.AddWithValue("@newClientCompanyName", newClientCompanyName);

                    int count = (int)checkCommand.ExecuteScalar();
                    if (count > 0)
                    {
                        Console.WriteLine("User already exists with the provided email or company name.");
                        return new JsonResult("Fail");
                    }
                }

                // Insert new client and retrieve the clientID
                string insertQuery = @"
                    INSERT INTO dbo.Clients (clientFirstName, clientLastName, clientCompanyName, clientCompanyAddress, clientCompanyCity, clientCompanyState, clientCompanyZipCode, clientPhoneNumber, clientEmail, clientPassword) 
                    OUTPUT INSERTED.clientID 
                    VALUES(@newClientFName, @newClientLName, @newClientCompanyName, @newClientCompanyAddress, @newClientCompanyCity, @newClientCompanyState, @newClientCompanyZipCode, @newClientPhoneNumber, @newClientEmail, @newClientPassword)";
                
                using (SqlCommand insertCommand = new SqlCommand(insertQuery, myCon))
                {
                    insertCommand.Parameters.AddWithValue("@newClientFName", newClientFName.ToUpper());
                    insertCommand.Parameters.AddWithValue("@newClientLName", newClientLName.ToUpper());
                    insertCommand.Parameters.AddWithValue("@newClientCompanyName", newClientCompanyName.ToUpper());
                    insertCommand.Parameters.AddWithValue("@newClientCompanyAddress", newClientCompanyAddress.ToUpper());
                    insertCommand.Parameters.AddWithValue("@newClientCompanyCity", newClientCompanyCity.ToUpper());
                    insertCommand.Parameters.AddWithValue("@newClientCompanyState", newClientCompanyState.ToUpper());
                    insertCommand.Parameters.AddWithValue("@newClientCompanyZipCode", newClientCompanyZipCode);
                    insertCommand.Parameters.AddWithValue("@newClientPhoneNumber", newClientPhoneNumber);
                    insertCommand.Parameters.AddWithValue("@newClientEmail", newClientEmail.ToLower());
                    insertCommand.Parameters.AddWithValue("@newClientPassword", hashedPassword);

                    newClientID = (int)insertCommand.ExecuteScalar();
                }

                myCon.Close();
            }
            Console.WriteLine("Client Successfully created.");

            var token = GenerateJwtToken(newClientEmail);
            var userInfo = new
            {
                ClientID = newClientID,
                Email = newClientEmail,
                FirstName = newClientFName,
                LastName = newClientLName,
                Company = newClientCompanyName,
                CompanyAddress = newClientCompanyAddress,
                CompanyCity = newClientCompanyCity,
                CompanyState = newClientCompanyState,
                CompanyZipCode = newClientCompanyZipCode,
                CompanyPhone = newClientPhoneNumber,
                Token = token
            };
            return new JsonResult(userInfo);
        }

        private string GenerateJwtToken(string clientEmail)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, clientEmail),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Issuer"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(30),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
