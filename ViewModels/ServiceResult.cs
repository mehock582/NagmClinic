namespace NagmClinic.ViewModels
{
    public class ServiceResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        public static ServiceResult SuccessResult(string message = "") => new ServiceResult { Success = true, Message = message };
        public static ServiceResult Error(string message) => new ServiceResult { Success = false, Message = message };
    }

    public class ServiceResult<T> : ServiceResult
    {
        public T? Data { get; set; }

        public static ServiceResult<T> SuccessResult(T data, string message = "") => new ServiceResult<T> { Success = true, Data = data, Message = message };
        public new static ServiceResult<T> Error(string message) => new ServiceResult<T> { Success = false, Message = message };
    }
}
