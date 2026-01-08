using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookShoppingCartMvcUI.Models.DTOs;
using BookShoppingCartMvcUI.Services;
using BookShoppingCartMvcUI.Data;
using BookShoppingCartMvcUI.Models;

namespace BookShoppingCartMvcUI.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly ICartRepository _cartRepo;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly VnPayService _vnPayService;
        private readonly ApplicationDbContext _context;

        public CartController(
            ICartRepository cartRepo,
            UserManager<IdentityUser> userManager,
            VnPayService vnPayService,
            ApplicationDbContext context)
        {
            _cartRepo = cartRepo;
            _userManager = userManager;
            _vnPayService = vnPayService;
            _context = context;
        }

        // ✅ Add to Cart
        public async Task<IActionResult> AddItem(int bookId, int qty = 1, int redirect = 0)
        {
            var cartCount = await _cartRepo.AddItem(bookId, qty);

            var userId = _userManager.GetUserId(User);
            if (!string.IsNullOrEmpty(userId))
            {
                var existing = await _context.UserInteractions
                    .FirstOrDefaultAsync(x => x.UserId == userId && x.BookId == bookId);

                if (existing == null)
                {
                    var interaction = new UserInteraction
                    {
                        UserId = userId,
                        BookId = bookId,
                        Score = 3, // AddToCart = 3
                        InteractionDate = DateTime.Now
                    };
                    _context.UserInteractions.Add(interaction);
                }
                else
                {
                    existing.Score = (existing.Score ?? 0) + 3;
                    existing.InteractionDate = DateTime.Now;
                    _context.UserInteractions.Update(existing);
                }
                await _context.SaveChangesAsync();
            }

            if (redirect == 0)
                return Ok(cartCount);
            return RedirectToAction("GetUserCart");
        }

        // ✅ Remove from Cart
        public async Task<IActionResult> RemoveItem(int bookId)
        {
            await _cartRepo.RemoveItem(bookId);
            return RedirectToAction("GetUserCart");
        }

        public async Task<IActionResult> GetUserCart()
        {
            var cart = await _cartRepo.GetUserCart();
            return View(cart);
        }

        public async Task<IActionResult> GetTotalItemInCart()
        {
            int cartItem = await _cartRepo.GetCartItemCount();
            return Ok(cartItem);
        }

        // ✅ GET: Checkout
        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var model = new CheckoutModel();
            var user = await _userManager.GetUserAsync(User);

            if (user != null)
            {
                model.Email = user.Email;
                model.MobileNumber = await _userManager.GetPhoneNumberAsync(user);
            }

            return View(model);
        }

        // ✅ POST: Checkout (COD only)
        [HttpPost]
        public async Task<IActionResult> Checkout(CheckoutModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var userId = _userManager.GetUserId(User);

            var cartItemsBeforeCheckout = await _cartRepo.GetUserCart();
            var purchasedBooks = (cartItemsBeforeCheckout?.CartDetails
                .Select(cd => new { cd.BookId, cd.Quantity })
                .ToList()) ?? Enumerable.Empty<object>().Select(_ => new { BookId = 0, Quantity = 0 }).ToList();

            bool isCheckedOut = await _cartRepo.DoCheckout(model);

            if (isCheckedOut && purchasedBooks.Any())
            {
                foreach (var item in purchasedBooks)
                {
                    var existing = await _context.UserInteractions
                        .FirstOrDefaultAsync(x => x.UserId == userId && x.BookId == item.BookId);

                    if (existing == null)
                    {
                        var interaction = new UserInteraction
                        {
                            UserId = userId,
                            BookId = item.BookId,
                            Score = 5, // Purchase = 5
                            InteractionDate = DateTime.Now
                        };
                        _context.UserInteractions.Add(interaction);
                    }
                    else
                    {
                        existing.Score = (existing.Score ?? 0) + 5;
                        existing.InteractionDate = DateTime.Now;
                        _context.UserInteractions.Update(existing);
                    }
                }
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(OrderSuccess));
            }

            return RedirectToAction(nameof(OrderFailure));
        }


        // =======================================
        // 🔵 PAY WITH VNPAY
        // =======================================
        [HttpPost]
        public async Task<IActionResult> PayWithVnPay(CheckoutModel model)
        {
            if (!ModelState.IsValid)
                return View("Checkout", model);

            var userId = _userManager.GetUserId(User);
            var cart = await _cartRepo.GetUserCart();

            if (cart == null || !cart.CartDetails.Any())
                return RedirectToAction("GetUserCart");

            // 1) Tạo đơn hàng
            var order = new Order
            {
                UserId = userId,
                Name = model.Name,
                Email = model.Email,
                MobileNumber = model.MobileNumber,
                Address = model.Address,
                PaymentMethod = "VNPAY",
                OrderStatusId = 1,         // 1 = Chờ thanh toán
                TotalAmount = await _cartRepo.GetCartTotal()

            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();   // Lúc này order.Id được tạo

            // 2) Tạo URL thanh toán
            string paymentUrl = _vnPayService.CreatePaymentUrl(order.Id, order.TotalAmount, HttpContext);

            return Redirect(paymentUrl);
        }
        // =======================================
        // 🔵 VNPAY RETURN
        // =======================================
        [HttpGet]
        public async Task<IActionResult> VnPayReturn()
        {
            var query = Request.Query;

            // 1) Validate chữ ký
            bool isValid = _vnPayService.ValidateResponse(query);
            if (!isValid)
            {
                ViewBag.Msg = "Chữ ký VNPay không hợp lệ.";
                return View("PaymentFail");
            }

            string orderId = query["vnp_TxnRef"];
            string responseCode = query["vnp_ResponseCode"];
            string bankCode = query["vnp_BankCode"];
            string transNo = query["vnp_TransactionNo"];
            string payDate = query["vnp_PayDate"];

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == int.Parse(orderId));

            if (order == null)
            {
                ViewBag.Msg = "Không tìm thấy đơn hàng.";
                return View("PaymentFail");
            }

            if (responseCode == "00") // Thành công
            {
                order.IsPaid = true;
                order.PaymentMethod = "VNPAY";
                order.Vnp_BankCode = bankCode;
                order.Vnp_TransactionNo = transNo;
                order.Vnp_PayDate = payDate;
                order.OrderStatusId = 2;  // 2 = Đã thanh toán

                // Xóa giỏ hàng user
                await _cartRepo.ClearCart();

                await _context.SaveChangesAsync();

                return View("PaymentSuccess", order);
            }

            ViewBag.Msg = "Thanh toán thất bại. Mã lỗi: " + responseCode;
            return View("PaymentFail");
        }

        public IActionResult OrderSuccess()
        {
            return View();
        }

        public IActionResult OrderFailure()
        {
            return View();
        }
    }
}
