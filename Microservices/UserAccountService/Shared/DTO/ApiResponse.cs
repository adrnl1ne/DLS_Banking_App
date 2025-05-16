using System;

namespace UserAccountService.Shared.DTO
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }  // Made nullable
        public string? ErrorCode { get; set; }  // Made nullable
        public T? Data { get; set; }  // Made nullable
    }
}