using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Stamon.Services;

namespace StamonTester.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {

        private StamonService _stamon;

        public TestController(StamonService stamon)
        {
            _stamon = stamon;
        }

        [HttpGet]
        public async Task<ActionResult> Test()
        {
            await _stamon.StatisticEvent("Request");
            await _stamon.StatisticEvent("Test");
            return Ok();
        }
    }
}
