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
         * UPDATEUSERINFO 
         * This method responsible for updating any and all of the 
         * existing user information such name, lastname, email, company, etc.
         */
        [HttpPost]
        [Route("UpdateUserInfo")]
        public JsonResult UpdateUserInfo([FromBody] Dictionary<string, object> updatedData)
        {
            Console.WriteLine(updatedData);
            // Check if the updatedData dictionary is null or empty
            if (updatedData == null || updatedData.Count == 0)
            {
                Console.WriteLine("1 " + updatedData);
                return new JsonResult("error 1: No data to update"); // Return a 400 Bad Request response if there's no data to update
            }

            // Retrieve the clientID from the updatedData dictionary
            // .ContainsKey(); boolean method determines whether the Dictionary<TKey,TValue> contains the specified key, i.e. ClientID
            string clientID = updatedData.ContainsKey("ClientID") ? updatedData["ClientID"].ToString() : null;

            Console.WriteLine(clientID);

            // Check if the clientID is null or empty
            if (string.IsNullOrEmpty(clientID))
            {
                Console.WriteLine("2 " + clientID);
                return new JsonResult("error 2: ClientID is required"); // Return error message if ClientID is missing
            }

            // Initialize the base update query for the Users table
            string updateQuery = "UPDATE dbo.Clients SET ";

            // Create a list to hold the SET clauses for the update query
            List<string> updateClauses = new List<string>();

            // Create a list to hold the SQL parameters for the update query
            List<SqlParameter> parameters = new List<SqlParameter>();

            // Iterate over the keys in the updatedData dictionary
            foreach (var key in updatedData.Keys)
            {
                Console.WriteLine(key);
                // Skip the ClientID key as it should NOT be updated
                if (key != "ClientID")
                {
                    Console.WriteLine("3");
                    // Add the SET clause for the current key to the list of update clauses
                    updateClauses.Add($"{key} = @{key}");

                    // Add a new SQL parameter for the current key-value pair to the list of parameters
                    parameters.Add(new SqlParameter($"@{key}", updatedData[key] ?? DBNull.Value));
                }
            }

            // Join the update clauses into a single string and append to the base update query
            updateQuery += string.Join(", ", updateClauses);

            // Append the WHERE clause to the update query to target the specific clientID
            updateQuery += " WHERE ClientID = @ClientID";

            // Add the clientID parameter to the list of parameters
            parameters.Add(new SqlParameter("@ClientID", clientID));

            try
            {
                Console.WriteLine("4");
                // Retrieve the database connection string from the configuration
                string sqlDatasource = _configuration.GetConnectionString("invosmartDBCon");

                using (SqlConnection myCon = new SqlConnection(sqlDatasource))
                {
                    myCon.Open(); // Open the database connection

                    // Create a new SQL command with the update query and connection
                    using (SqlCommand myCommand = new SqlCommand(updateQuery, myCon))
                    {
                        // Add the parameters to the SQL command
                        myCommand.Parameters.AddRange(parameters.ToArray());

                        // Execute the update query
                        myCommand.ExecuteNonQuery();
                    }
                }

                // Return a SUCCESS response indicating success
                return new JsonResult("SUCCESS");
            }
            catch (Exception ex)
            {
                // Log the exception message to the console for debugging
                Console.WriteLine($"Error updating user info: {ex.Message}");

                // Return a 500 Internal Server Error response indicating a server-side error
                return new JsonResult("error 3: Server Error");
            }
        }

        /**
         * LOGINCLIENT
         * This function is responsible for handling the login process for clients.
         */
        [HttpPost]
        [Route("LoginClient")]
        public JsonResult LoginClient([FromForm] string clientEmail, [FromForm] string clientPassword)
        {
            // Define the SQL query to retrieve client information based on the provided email
            string query = "SELECT clientID, clientFirstName, clientLastName, clientCompanyName, clientCompanyAddress, clientCompanyCity, clientCompanyState, clientCompanyZipCode, clientPhoneNumber, clientEmail, clientPassword FROM dbo.clients WHERE clientEmail = @clientEmail";

            // Create a DataTable to hold the result of the SQL query
            DataTable table = new DataTable();

            // Get the connection string for the database from the configuration file
            string sqlDatasource = _configuration.GetConnectionString("invosmartDBCon");

            // Declare a SqlDataReader to read the results of the query
            SqlDataReader myReader;

            // Open a connection to the SQL database
            using (SqlConnection myCon = new SqlConnection(sqlDatasource))
            {
                myCon.Open(); // Open the connection
                using (SqlCommand myCommand = new SqlCommand(query, myCon)) // Prepare the SQL command
                {
                    // Add the email parameter to the SQL query
                    myCommand.Parameters.AddWithValue("@clientEmail", clientEmail.ToLower());

                    // Execute the query and read the results
                    myReader = myCommand.ExecuteReader();

                    table.Load(myReader); // Load the results into the DataTable
                    myReader.Close(); // Close the reader
                    myCon.Close(); // Close the connection
                }
            }

            // Check if exactly one row was returned (i.e., a matching email was found)
            if (table.Rows.Count == 1)
            {
                Console.WriteLine("Email validated"); // Log that the email was validated

                DataRow row = table.Rows[0]; // Get the first row of the result
                string hashedPasswordFromDB = row["clientPassword"].ToString(); // Get the hashed password from the database

                // Verify the provided password against the hashed password from the database
                if (BCrypt.Net.BCrypt.Verify(clientPassword, hashedPasswordFromDB))
                {
                    Console.WriteLine("Password validated"); // Log that the password was validated

                    var token = GenerateJwtToken(clientEmail); // Generate a JWT token for the user
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
                    return new JsonResult(userInfo); // Return the user info and token as JSON
                }
            }

            Console.WriteLine("Incorrect Email or Password"); // Log that the login attempt failed
            return new JsonResult("Fail"); // Return a failure message as JSO
        }

        /**
         * SIGNUPCLIENT
         * This function is responsible for handling the sign-up process for new clients.
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
                    // Add parameters to the SQL query
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

                    // Execute the query and get the new client ID
                    newClientID = (int)insertCommand.ExecuteScalar();
                }

                myCon.Close(); // Close the connection
            }
            Console.WriteLine("Client Successfully created."); // Log the successful creation of the client

            // Generate a JWT token for the new client
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
            return new JsonResult(userInfo); // Return the new client info and token as JSON
        }

        /**
         * JWTToken
         * This method is responsible for generating a JSON Web Token (JWT) for a given client email.
         * The JWT token is used to authenticate the client in subsequent requests.
         * The resulting token can be used to securely identify and authenticate the client during interactions with the server.
         */
        private string GenerateJwtToken(string clientEmail)
        {
            // Create a security key using the secret key from the configuration
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));

            // Create signing credentials using the security key and HMAC-SHA256 algorithm
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            // Define the claims to be included in the JWT token
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, clientEmail),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Create the JWT token with the specified issuer, audience, claims, expiration time, and signing credentials
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Issuer"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(30),
                signingCredentials: credentials);

            // Write the token to a string and return it
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
