using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;

namespace InvoSmart.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvoiceController : ControllerBase
    {
        private IConfiguration _configuration;

        public InvoiceController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost]
        [Route("CreateInvoice")]
        public JsonResult CreateInvoice([FromBody] InvoiceData invoiceData)
        {
            string sqlDatasource = _configuration.GetConnectionString("invosmartDBCon");

            using (SqlConnection myCon = new SqlConnection(sqlDatasource))
            {
                myCon.Open();

                // Check if clientID exists
                string checkClientQuery = "SELECT COUNT(1) FROM dbo.Clients WHERE clientID = @clientID";
                using (SqlCommand checkClientCommand = new SqlCommand(checkClientQuery, myCon))
                {
                    checkClientCommand.Parameters.AddWithValue("@clientID", invoiceData.ClientID);
                    int clientExists = (int)checkClientCommand.ExecuteScalar();

                    if (clientExists == 0)
                    {
                        return new JsonResult("Client does not exist");
                    }
                }

                // Insert into Invoices table
                string invoiceInsertQuery = @"INSERT INTO dbo.Invoices 
                                            (clientID, invoiceNumber, invoiceDate, dueDate, billedToEntityName, billedToEntityAddress, payableTo, servicesRendered, submittedOn, subTotal, total) 
                                            OUTPUT INSERTED.invoiceID 
                                            VALUES (@clientID, @invoiceNumber, @invoiceDate, @dueDate, @billedToEntityName, @billedToEntityAddress, @payableTo, @servicesRendered, @submittedOn, @subTotal, @total)";

                int invoiceID;
                using (SqlCommand invoiceInsertCommand = new SqlCommand(invoiceInsertQuery, myCon))
                {
                    invoiceInsertCommand.Parameters.AddWithValue("@clientID", invoiceData.ClientID);
                    invoiceInsertCommand.Parameters.AddWithValue("@invoiceNumber", invoiceData.InvoiceNumber);
                    invoiceInsertCommand.Parameters.AddWithValue("@invoiceDate", invoiceData.InvoiceDate);
                    invoiceInsertCommand.Parameters.AddWithValue("@dueDate", invoiceData.DueDate);
                    invoiceInsertCommand.Parameters.AddWithValue("@billedToEntityName", invoiceData.BilledToEntityName);
                    invoiceInsertCommand.Parameters.AddWithValue("@billedToEntityAddress", invoiceData.BilledToEntityAddress);
                    invoiceInsertCommand.Parameters.AddWithValue("@payableTo", invoiceData.PayableTo);
                    invoiceInsertCommand.Parameters.AddWithValue("@servicesRendered", invoiceData.ServicesRendered);
                    invoiceInsertCommand.Parameters.AddWithValue("@submittedOn", invoiceData.SubmittedOn);
                    invoiceInsertCommand.Parameters.AddWithValue("@subTotal", invoiceData.SubTotal);
                    invoiceInsertCommand.Parameters.AddWithValue("@total", invoiceData.Total);

                    invoiceID = (int)invoiceInsertCommand.ExecuteScalar();
                }

                // Insert into InvoiceItems table
                string invoiceItemsInsertQuery = @"
            INSERT INTO dbo.InvoiceItems 
            (invoiceID, description, address, qty, unitPrice) 
            VALUES (@invoiceID, @description, @address, @qty, @unitPrice)";

                foreach (var item in invoiceData.Items)
                {
                    using (SqlCommand invoiceItemsInsertCommand = new SqlCommand(invoiceItemsInsertQuery, myCon))
                    {
                        invoiceItemsInsertCommand.Parameters.AddWithValue("@invoiceID", invoiceID);
                        invoiceItemsInsertCommand.Parameters.AddWithValue("@description", item.Description);
                        invoiceItemsInsertCommand.Parameters.AddWithValue("@address", item.Address);
                        invoiceItemsInsertCommand.Parameters.AddWithValue("@qty", item.Qty);
                        invoiceItemsInsertCommand.Parameters.AddWithValue("@unitPrice", item.UnitPrice);

                        invoiceItemsInsertCommand.ExecuteNonQuery();
                    }
                }

                myCon.Close();
            }

            return new JsonResult("Invoice created successfully");
        }
    }

    public class InvoiceData
    {
        public int ClientID { get; set; } = 1; // Dummy value: 1
        public string InvoiceNumber { get; set; } = "INV-12345"; // Dummy value: "INV-12345"
        public DateTime InvoiceDate { get; set; } = DateTime.Parse("2024-07-02"); // Dummy value: "2024-07-02"
        public DateTime DueDate { get; set; } = DateTime.Parse("2024-07-15"); // Dummy value: "2024-07-15"
        public string BilledToEntityName { get; set; } = "ABC Corp"; // Dummy value: "ABC Corp"
        public string BilledToEntityAddress { get; set; } = "123 Main St, Springfield"; // Dummy value: "123 Main St, Springfield"
        public string PayableTo { get; set; } = "XYZ Services"; // Dummy value: "XYZ Services"
        public string ServicesRendered { get; set; } = "Web development"; // Dummy value: "Web development"
        public DateTime SubmittedOn { get; set; } = DateTime.Parse("2024-07-02"); // Dummy value: "2024-07-02"
        public decimal SubTotal { get; set; } = 1500.00M; // Dummy value: 1500.00
        public decimal Total { get; set; } = 1650.00M; // Dummy value: 1650.00
        public List<InvoiceItem> Items { get; set; } = new List<InvoiceItem>
        {
        new InvoiceItem
        {
            Description = "Website design", // Dummy value: "Website design"
            Address = "123 Main St", // Dummy value: "123 Main St"
            Qty = 1, // Dummy value: 1
            UnitPrice = 500.00M, // Dummy value: 500.00
        }
        };
    }

    public class InvoiceItem
    {
        public string Description { get; set; } = "Sample Description"; // Dummy value: "Sample Description"
        public string Address { get; set; } = "Sample Address"; // Dummy value: "Sample Address"
        public int Qty { get; set; } = 1; // Dummy value: 1
        public decimal UnitPrice { get; set; } = 100.00M; // Dummy value: 100.00
    }
}
