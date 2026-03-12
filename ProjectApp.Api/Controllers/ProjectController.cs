using ProjectApp.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using ProjectApp.Api.Services;

namespace ProjectApp.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProjectController(ProgramProjectGeneratorService generatorService, ILogger<ProjectController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProgramProject>> GetById([FromQuery] int id, CancellationToken cancellationToken)
    {
        if (id < 0)
        {
            return BadRequest("id must be a positive integer.");
        }

        logger.LogInformation("Received request to retrieve/generate project {Id}", id);

        var project = await generatorService.GetByIdAsync(id, cancellationToken);

        return Ok(project);
    }
}
