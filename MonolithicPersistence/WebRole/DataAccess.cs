﻿using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using WebRole.Models;

namespace WebRole
{
    public class DataAccess
    {
        static string eventHubName = CloudConfigurationManager.GetSetting("EventHubName");
        static string eventHubNamespace = CloudConfigurationManager.GetSetting("EventHubNamespace");
        static string devicesSharedAccessPolicyName = CloudConfigurationManager.GetSetting("LogPolicyName");
        static string devicesSharedAccessPolicyKey = CloudConfigurationManager.GetSetting("LogPolicyKey");
        static string eventHubConnectionString = string.Format("Endpoint=sb://{0}.servicebus.windows.net/;SharedAccessKeyName={1};SharedAccessKey={2};TransportType=Amqp",
            eventHubNamespace, devicesSharedAccessPolicyName, devicesSharedAccessPolicyKey);
        static EventHubClient client = EventHubClient.CreateFromConnectionString(eventHubConnectionString, eventHubName);

        private static string sqlDBConnectionString = CloudConfigurationManager.GetSetting("SQLDBConnectionString");
        public static async Task InsertToPurchaseOrderHeaderTableAsync()
        {
            string queryString =
                    "INSERT INTO Purchasing.PurchaseOrderHeader(" +
                    " RevisionNumber, Status, EmployeeID, VendorID, ShipMethodID, OrderDate, ShipDate, SubTotal, TaxAmt, Freight, ModifiedDate)" +
                    " VALUES(" +
                    "@RevisionNumber,@Status,@EmployeeID,@VendorID,@ShipMethodID,@OrderDate,@ShipDate,@SubTotal,@TaxAmt,@Freight,@ModifiedDate)";
            var dt = DateTime.UtcNow;
            using (SqlConnection cn = new SqlConnection(sqlDBConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand(queryString, cn))
                {
                    cmd.Parameters.Add("@RevisionNumber", SqlDbType.TinyInt).Value = 1;
                    cmd.Parameters.Add("@Status", SqlDbType.TinyInt).Value = 4;
                    cmd.Parameters.Add("@EmployeeID", SqlDbType.Int).Value = 258;
                    cmd.Parameters.Add("@VendorID", SqlDbType.Int).Value = 1580;
                    cmd.Parameters.Add("@ShipMethodID", SqlDbType.Int).Value = 3;
                    cmd.Parameters.Add("@OrderDate", SqlDbType.DateTime).Value = dt;
                    cmd.Parameters.Add("@ShipDate", SqlDbType.DateTime).Value = dt;
                    cmd.Parameters.Add("@SubTotal", SqlDbType.Money).Value = 123.40;
                    cmd.Parameters.Add("@TaxAmt", SqlDbType.Money).Value = 12.34;
                    cmd.Parameters.Add("@Freight", SqlDbType.Money).Value = 5.76;
                    cmd.Parameters.Add("@ModifiedDate", SqlDbType.DateTime).Value = dt;

                    await cn.OpenAsync();
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        public static async Task<string> SelectProductDescriptionAsync(int id)
        {
            string result = "";
            string queryString = "SELECT Description FROM Production.ProductDescription WHERE ProductDescriptionID=@inputId";
            using (SqlConnection cn = new SqlConnection(sqlDBConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand(queryString, cn))
                {
                    cmd.Parameters.AddWithValue("@inputId", id);
                    await cn.OpenAsync();
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            result = reader.GetFieldValue<string>(0); ;
                        }
                    }
                }
            }
            return result;
        }

        public static async Task LogToSqldbAsync(LogMessage logMessage)
        {
            string queryString = "INSERT INTO dbo.SqldbLog(Message, LogId, LogTime) VALUES(@Message, @LogId, @LogTime)";
            using (SqlConnection cn = new SqlConnection(sqlDBConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand(queryString, cn))
                {
                    cmd.Parameters.Add("@LogId", SqlDbType.NChar, 32).Value = logMessage.LogId;
                    cmd.Parameters.Add("@Message", SqlDbType.NText).Value = logMessage.Message;
                    cmd.Parameters.Add("@LogTime", SqlDbType.DateTime).Value = logMessage.LogTime;
                    await cn.OpenAsync();
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        public static async Task LogToEventhubAsync(LogMessage logMessage)
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings();
            var serializedString = JsonConvert.SerializeObject(logMessage);
            var bytes = Encoding.UTF8.GetBytes(serializedString);

            using (EventData data = new EventData(bytes))
            {
                await client.SendAsync(data).ConfigureAwait(false);
            }
        }

    }
}