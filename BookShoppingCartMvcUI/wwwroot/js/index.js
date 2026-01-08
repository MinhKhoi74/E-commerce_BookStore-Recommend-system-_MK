/* Swiper: Carousel chính */
const swiper = new Swiper(".mySwiper", {
    slidesPerView: 5,
    spaceBetween: 20,
    loop: true,
    navigation: {
        nextEl: ".swiper-button-next",
        prevEl: ".swiper-button-prev",
    },
    autoplay: {
        delay: 4000,
        disableOnInteraction: false,
    },
    breakpoints: {
        320: { slidesPerView: 2 },
        576: { slidesPerView: 3 },
        768: { slidesPerView: 4 },
        992: { slidesPerView: 5 },
    },
});


/* Hiển thị sách (bảng xếp hạng) */
function selectBook(imagePath) {
    console.log("📸 Hàm selectBook() được gọi với imagePath:", imagePath);

    const imageElement = document.getElementById("selectedBookImage");
    if (!imageElement) {
        console.error("❌ Không tìm thấy phần tử #selectedBookImage");
        return;
    }

    if (imagePath && imagePath.trim() !== "") {
        if (!imagePath.startsWith("/")) {
            imagePath = "/images/" + imagePath;
        }

        console.log("✅ Đang hiển thị ảnh:", imagePath);
        imageElement.src = imagePath;
    } else {
        console.warn("⚠️ Không có đường dẫn ảnh. Sử dụng ảnh mặc định.");
        imageElement.src = "/images/NoImage.png";
    }
}




