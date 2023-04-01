using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Stamon.Services;

namespace Stamon.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StamonController : ControllerBase
    {
        private readonly StamonService _slotService;

        public StamonController(StamonService service)
        {
            _slotService = service;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("Slot/{slotName}/Data/{monitorName}")]
        public async Task<ActionResult> GetSlot(string slotName, string monitorName)
        {
            return Ok(await _slotService.GetSlotDatas(slotName, monitorName));
        }


        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("Slot/Names")]
        public async Task<ActionResult> GetSlotNames()
        {
            return Ok(await _slotService.GetSlotNames());
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("Slot/Events")]
        public async Task<ActionResult> GetEventNames()
        {
            return Ok(await _slotService.GetEventNames());
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("Event/Internal/{name}")]
        public async Task<ActionResult> AddEvent(string name)
        {
            await _slotService.StatisticEvent(name);
            return Ok();
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("Event/Internal")]
        public async Task<ActionResult> AddEventList([FromBody]List<string> events)
        {
            if (events is { Count: > 0 })
            {
                foreach (var @event in events)
                {
                    await _slotService.StatisticEvent(@event);
                }
            }
            
            return Ok();
        }

    }
}
