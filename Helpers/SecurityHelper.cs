using System;
using System.Security.Cryptography;
using System.Text;

namespace NewsPortal.Helpers
{
    public static class SecurityHelper
    {
        /// <summary>
        /// Mã hóa mật khẩu một chiều bằng thuật toán SHA-256 (trả về chuỗi hex 64 ký tự chữ thường).
        /// </summary>
        public static string HashPassword(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return string.Empty;
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(plainText);
                byte[] hashBytes = sha256.ComputeHash(bytes);

                StringBuilder sb = new StringBuilder(hashBytes.Length * 2);
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Kiểm tra mật khẩu người dùng nhập vào so với giá trị lưu trong CSDL.
        /// Hỗ trợ so khớp mã băm SHA-256 và tương thích ngược nếu dữ liệu test nhập tay dạng thuần.
        /// </summary>
        public static bool VerifyPassword(string plainInput, string storedPasswordHash, out bool needsRehash)
        {
            needsRehash = false;
            if (string.IsNullOrEmpty(plainInput) || string.IsNullOrEmpty(storedPasswordHash))
            {
                return false;
            }

            string computedHash = HashPassword(plainInput);
            if (string.Equals(computedHash, storedPasswordHash.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Trường hợp dữ liệu mẫu trong SQL Server được chèn trực tiếp dạng plain-text khi test
            if (storedPasswordHash.Trim().Length != 64 && string.Equals(plainInput, storedPasswordHash, StringComparison.Ordinal))
            {
                needsRehash = true;
                return true;
            }

            return false;
        }
    }
}
