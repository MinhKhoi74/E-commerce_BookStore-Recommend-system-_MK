using Microsoft.AspNetCore.Mvc;
using BookShoppingCartMvcUI.Data;
using BookShoppingCartMvcUI.Services;
using Microsoft.EntityFrameworkCore;

namespace BookShoppingCartMvcUI.Controllers
{
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly VnPayService _vnPayService;

        public PaymentController(ApplicationDbContext context, VnPayService vnPayService)
        {
            _context = context;
            _vnPayService = vnPayService;
        }

        // ===============================
        // 1) TẠO URL THANH TOÁN VNPAY
        // ===============================
        [HttpGet]
        public async Task<IActionResult> Pay(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetail)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                return NotFound("Không tìm thấy đơn hàng");

            if (order.IsPaid)
                return BadRequest("Đơn hàng đã thanh toán.");

            // Tạo URL thanh toán
            var paymentUrl = _vnPayService.CreatePaymentUrl(order.Id, order.TotalAmount, HttpContext);

            return Redirect(paymentUrl);
        }

        // ========================================
        // 2) VNPAY TRẢ VỀ (ReturnUrl)
        // ========================================
        [HttpGet]
        public async Task<IActionResult> PaymentReturn()
        {
            var query = Request.Query;

            // Kiểm tra chữ ký
            bool isValid = _vnPayService.ValidateResponse(query);

            if (!isValid)
            {
                ViewBag.Msg = "Chữ ký không hợp lệ - nghi ngờ giả mạo dữ liệu.";
                return View("PaymentFail");
            }

            // Lấy thông tin từ query
            string orderId = query["vnp_TxnRef"];
            string vnpResponseCode = query["vnp_ResponseCode"]; // 00 = thành công
            string vnpBankCode = query["vnp_BankCode"];
            string vnpTransactionNo = query["vnp_TransactionNo"];
            string vnpPayDate = query["vnp_PayDate"];

            var order = await _context.Orders.FirstOrDefaultAsync(x => x.Id == int.Parse(orderId));

            if (order == null)
            {
                ViewBag.Msg = "Đơn hàng không tồn tại.";
                return View("PaymentFail");
            }

            if (vnpResponseCode == "00") // Thành công
            {
                order.IsPaid = true;
                order.PaymentMethod = "VNPAY";
                order.Vnp_BankCode = vnpBankCode;
                order.Vnp_TransactionNo = vnpTransactionNo;
                order.Vnp_PayDate = vnpPayDate;
                order.OrderStatusId = 2; // Ví dụ: 1 = mới tạo, 2 = đã thanh toán

                await _context.SaveChangesAsync();

                return View("PaymentSuccess", order);
            }
            else
            {
                ViewBag.Msg = "Thanh toán thất bại. Mã lỗi: " + vnpResponseCode;
                return View("PaymentFail");
            }
        }
    }
}
