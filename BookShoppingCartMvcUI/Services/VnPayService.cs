using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace BookShoppingCartMvcUI.Services
{
    public class VnPayService
    {
        private readonly IConfiguration _config;

        public VnPayService(IConfiguration config)
        {
            _config = config;
        }

        public string CreatePaymentUrl(int orderId, decimal amount, HttpContext context)
        {
            var vnpUrl = _config["VnPay:Url"];
            var returnUrl = _config["VnPay:ReturnUrl"];
            var tmnCode = _config["VnPay:TmnCode"];
            var hashSecret = _config["VnPay:HashSecret"];

            var tick = DateTime.Now.Ticks.ToString();

            // VNPay yêu cầu tiền phải nhân 100
            var price = (long)(amount * 100);

            Dictionary<string, string> vnpParams = new()
            {
                { "vnp_Version", "2.1.0" },
                { "vnp_Command", "pay" },
                { "vnp_TmnCode", tmnCode },
                { "vnp_Amount", price.ToString() },
                { "vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss") },
                { "vnp_CurrCode", "VND" },
                { "vnp_IpAddr", context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1" },
                { "vnp_Locale", "vn" },
                { "vnp_OrderInfo", $"Thanh toan don hang {orderId}" },
                { "vnp_OrderType", "other" },
                { "vnp_ReturnUrl", returnUrl },
                { "vnp_TxnRef", orderId.ToString() }
            };

            // Sắp xếp theo alphabet
            var sortedParams = vnpParams.OrderBy(x => x.Key);

            // Tạo chuỗi hash
            string data = string.Join("&", sortedParams
                .Select(kvp => $"{kvp.Key}={kvp.Value}"));

            string secureHash = HmacSHA512(hashSecret, data);

            // Tạo URL query
            var urlWithQuery = QueryHelpers.AddQueryString(vnpUrl, vnpParams);

            return $"{urlWithQuery}&vnp_SecureHash={secureHash}";
        }

        // Xác minh dữ liệu trả về từ VNPay
        public bool ValidateResponse(IQueryCollection query)
        {
            var hashSecret = _config["VnPay:HashSecret"];

            // Lấy secure hash từ query
            string vnpSecureHash = query["vnp_SecureHash"];

            // Lọc bỏ vnp_SecureHash và vnp_SecureHashType
            var vnpData = query
                .Where(q => q.Key.StartsWith("vnp_") && q.Key != "vnp_SecureHash" && q.Key != "vnp_SecureHashType")
                .ToDictionary(x => x.Key, x => x.Value.ToString());

            // Sort alphabet
            var sortedParams = vnpData.OrderBy(x => x.Key);

            string data = string.Join("&", sortedParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));

            string checkHash = HmacSHA512(hashSecret, data);

            return vnpSecureHash == checkHash;
        }

        private static string HmacSHA512(string key, string data)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
            byte[] hashValue = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return BitConverter.ToString(hashValue).Replace("-", "").ToUpper();
        }
    }
}
