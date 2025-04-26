using Microsoft.AspNetCore.Mvc;

namespace QueryService;

[ApiController]
[Route("api/[controller]")]
public class MainController : Controller
{

    private readonly RabbitMqConnection _rabbitConn;
    
    public MainController(RabbitMqConnection rabbitConn)
    {
        _rabbitConn = rabbitConn;
    }
    
    public IActionResult Index()
    {
        // Use the connection
        
        return View();
    }

}