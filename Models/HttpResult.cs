using System.Net;

public class HttpResult<T>
{
    public HttpStatusCode StatusCode { get; set; }
    public string? RawBody { get; set; }
    public T? Data { get; set; }
    public bool IsSuccess => (int)StatusCode is >= 200 and < 300;
}
