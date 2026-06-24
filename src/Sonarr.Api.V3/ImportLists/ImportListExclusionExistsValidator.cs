using FluentValidation;
using FluentValidation.Validators;
using NzbDrone.Core.ImportLists.Exclusions;

namespace Sonarr.Api.V3.ImportLists
{
    public class ImportListExclusionExistsValidator<T> : PropertyValidator<T, int>
    {
        private readonly IImportListExclusionService _importListExclusionService;

        public ImportListExclusionExistsValidator(IImportListExclusionService importListExclusionService)
        {
            _importListExclusionService = importListExclusionService;
        }

        public override string Name => "ImportListExclusionExistsValidator";

        protected override string GetDefaultMessageTemplate(string errorCode) => "This exclusion has already been added.";

        public override bool IsValid(ValidationContext<T> context, int value)
        {
            if (context.InstanceToValidate is not ImportListExclusionResource listExclusionResource)
            {
                return true;
            }

            return !_importListExclusionService.All().Exists(v => v.TvdbId == value && v.Id != listExclusionResource.Id);
        }
    }
}
