using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.PostgresMigration;
using Sonarr.Http;

namespace Sonarr.Api.V5.System.PostgresMigration;

[V5ApiController("system/postgres-migration")]
public class PostgresMigrationController : Controller
{
    private readonly IPostgresMigrationService _postgresMigrationService;
    private readonly IMainDatabase _mainDatabase;

    public PostgresMigrationController(IPostgresMigrationService postgresMigrationService,
                                       IMainDatabase mainDatabase)
    {
        _postgresMigrationService = postgresMigrationService;
        _mainDatabase = mainDatabase;
    }

    [HttpGet("status")]
    [Produces("application/json")]
    public ActionResult<PostgresMigrationStatusResource> GetStatus()
    {
        var status = _postgresMigrationService.GetStatus();

        return new PostgresMigrationStatusResource
        {
            State = status.State.ToString(),
            Step = status.Step,
            Message = status.Message,
            Error = status.Error,
            CurrentDatabaseType = _mainDatabase.DatabaseType.ToString(),
            RestartPending = status.RestartPending,
            RestartRequired = status.RestartRequired,
            Warnings = status.Warnings
        };
    }

    [HttpPost("validate")]
    [Produces("application/json")]
    public ActionResult<PostgresMigrationValidationResource> Validate([FromBody] PostgresMigrationConnectionResource resource)
    {
        PostgresMigrationValidationResult validation;

        try
        {
            validation = _postgresMigrationService.Validate(resource.ToModel());
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return new PostgresMigrationValidationResource
        {
            IsValid = validation.IsValid,
            Message = validation.Message,
            Warnings = validation.Warnings
        };
    }

    [HttpPost("start")]
    [Produces("application/json")]
    public ActionResult<PostgresMigrationStatusResource> Start([FromBody] PostgresMigrationConnectionResource resource)
    {
        PostgresMigrationJob status;

        try
        {
            status = _postgresMigrationService.Start(resource.ToModel());
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return new PostgresMigrationStatusResource
        {
            State = status.State.ToString(),
            Step = status.Step,
            Message = status.Message,
            Error = status.Error,
            CurrentDatabaseType = _mainDatabase.DatabaseType.ToString(),
            RestartPending = status.RestartPending,
            RestartRequired = status.RestartRequired,
            Warnings = status.Warnings
        };
    }
}
