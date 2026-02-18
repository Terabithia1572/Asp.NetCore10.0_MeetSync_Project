/* ============================================
   VideoConf Pro — Login Page JavaScript
   script.js
   ============================================ */

document.addEventListener('DOMContentLoaded', () => {

  /* ---- Elementleri Seç ---- */
  const loginForm     = document.getElementById('loginForm');
  const emailInput    = document.getElementById('email');
  const passwordInput = document.getElementById('password');
  const toggleBtn     = document.getElementById('togglePassword');
  const toggleIcon    = document.getElementById('toggleIcon');
  const submitBtn     = document.getElementById('submitBtn');
  const btnText       = document.getElementById('btnText');
  const btnIcon       = document.getElementById('btnIcon');


  /* ========================================
     1. Şifreyi Göster / Gizle
  ======================================== */
  toggleBtn.addEventListener('click', () => {
    const isPassword = passwordInput.type === 'password';

    passwordInput.type = isPassword ? 'text' : 'password';
    toggleIcon.textContent = isPassword ? 'visibility_off' : 'visibility';
  });


  /* ========================================
     2. Form Gönderimi & Basit Doğrulama
  ======================================== */
    /* script.js - İlgili Kısım */
    loginForm.addEventListener('submit', async (e) => {
        e.preventDefault(); // Tarayıcıyı durdur, ben kontrol edeceğim

        const email = emailInput.value.trim();
        const password = passwordInput.value;

        clearErrors();
        let hasError = false;

        // Senin validasyon kontrollerin (Email ve Şifre için olanlar kalsın)
        if (!email || !isValidEmail(email)) {
            showError(emailInput, 'Lütfen geçerli bir e-posta adresi girin.');
            hasError = true;
        }
        if (!password || password.length < 6) {
            showError(passwordInput, 'Şifre en az 6 karakter olmalıdır.');
            hasError = true;
        }

        if (hasError) return; // Hata varsa dur

        // ---- Loading Animasyonu Başlat ---- 
        setLoading(true);

        try {
            // Senin tasarladığın o 1 saniyelik havayı bozmayalım
            await delay(1000);

            // KRİTİK NOKTA: Formu gerçekten C# Controller'a gönderiyoruz
            loginForm.submit();

        } catch (err) {
            console.error('Login error:', err);
            setLoading(false);
        }
    });


  /* ========================================
     3. Yardımcı Fonksiyonlar
  ======================================== */

  /**
   * E-posta formatını kontrol eder.
   */
  function isValidEmail(email) {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
  }

  /**
   * Input altına hata mesajı ekler.
   */
  function showError(input, message) {
    input.classList.add('input-error');

    // Mevcut hata mesajı varsa tekrar ekleme
    let errorEl = input.parentElement.parentElement.querySelector('.error-msg');
    if (!errorEl) {
      errorEl = document.createElement('p');
      errorEl.classList.add('error-msg');
      input.parentElement.parentElement.appendChild(errorEl);
    }

    errorEl.textContent = message;
    errorEl.classList.add('visible');
  }

  /**
   * Tüm hata durumlarını temizler.
   */
  function clearErrors() {
    document.querySelectorAll('.input-error').forEach(el => el.classList.remove('input-error'));
    document.querySelectorAll('.error-msg').forEach(el => el.classList.remove('visible'));
  }

  /**
   * Butonu loading (yükleme) moduna alır veya çıkarır.
   */
  function setLoading(isLoading) {
    submitBtn.disabled = isLoading;

    if (isLoading) {
      btnText.textContent = 'Giriş yapılıyor...';
      btnIcon.textContent = 'hourglass_empty';
      submitBtn.classList.add('opacity-80', 'cursor-not-allowed');
    } else {
      btnText.textContent = 'Giriş Yap';
      btnIcon.textContent = 'arrow_forward';
      submitBtn.classList.remove('opacity-80', 'cursor-not-allowed');
    }
  }

  /**
   * Belirli ms kadar bekleyen basit Promise.
   */
  function delay(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
  }

});
