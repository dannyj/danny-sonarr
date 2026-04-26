using FluentValidation;
using NzbDrone.Core.Qualities;

namespace Sonarr.Api.V5.Profiles.Quality;

public class QualityProfileResourceValidator : AbstractValidator<QualityProfileResource>
{
    public QualityProfileResourceValidator()
    {
        RuleForEach(c => c.Items)
            .SetValidator(new QualityProfileQualityItemResourceValidator());
    }
}

public class QualityProfileQualityItemResourceValidator : AbstractValidator<QualityProfileQualityItemResource>
{
    public QualityProfileQualityItemResourceValidator()
    {
        RuleFor(c => c.MinSize)
            .GreaterThanOrEqualTo(QualityDefinitionLimits.Min)
            .WithErrorCode("GreaterThanOrEqualTo")
            .LessThanOrEqualTo(c => c.PreferredSize ?? QualityDefinitionLimits.Max)
            .WithErrorCode("LessThanOrEqualTo")
            .When(c => c.MinSize is not null);

        RuleFor(c => c.PreferredSize)
            .GreaterThanOrEqualTo(c => c.MinSize ?? QualityDefinitionLimits.Min)
            .WithErrorCode("GreaterThanOrEqualTo")
            .LessThanOrEqualTo(c => GetMaxSizeLimit(c) ?? QualityDefinitionLimits.Max)
            .WithErrorCode("LessThanOrEqualTo")
            .When(c => c.PreferredSize is not null);

        RuleFor(c => c.MaxSize)
            .GreaterThanOrEqualTo(c => c.PreferredSize ?? QualityDefinitionLimits.Min)
            .WithErrorCode("GreaterThanOrEqualTo")
            .LessThanOrEqualTo(QualityDefinitionLimits.Max)
            .WithErrorCode("LessThanOrEqualTo")
            .When(c => c.MaxSize is > 0);
    }

    private static double? GetMaxSizeLimit(QualityProfileQualityItemResource item)
    {
        return item.MaxSize is > 0 ? item.MaxSize : null;
    }
}
