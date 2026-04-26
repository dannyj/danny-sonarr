using FluentValidation.TestHelper;
using NUnit.Framework;
using NzbDrone.Core.Qualities;
using Sonarr.Api.V3.Profiles.Quality;

namespace NzbDrone.Api.Test.v3.Profiles.Quality;

[Parallelizable(ParallelScope.All)]
public class QualityProfileResourceValidatorTest
{
    private readonly QualityProfileQualityItemResourceValidator _validator = new();

    [Test]
    public void Validate_passes_when_max_size_zero_represents_unlimited()
    {
        var resource = new QualityProfileQualityItemResource
        {
            MinSize = 17.1,
            PreferredSize = 995,
            MaxSize = 0
        };

        var result = _validator.TestValidate(resource);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Validate_fails_when_min_size_is_above_preferred_size()
    {
        var resource = new QualityProfileQualityItemResource
        {
            MinSize = 10,
            PreferredSize = 5,
            MaxSize = null
        };

        var result = _validator.TestValidate(resource);

        result.ShouldHaveValidationErrorFor(r => r.MinSize)
            .WithErrorCode("LessThanOrEqualTo");
    }

    [Test]
    public void Validate_fails_when_max_size_exceeds_max_limit()
    {
        var resource = new QualityProfileQualityItemResource
        {
            MinSize = null,
            PreferredSize = null,
            MaxSize = QualityDefinitionLimits.Max + 1
        };

        var result = _validator.TestValidate(resource);

        result.ShouldHaveValidationErrorFor(r => r.MaxSize)
            .WithErrorCode("LessThanOrEqualTo");
    }
}
