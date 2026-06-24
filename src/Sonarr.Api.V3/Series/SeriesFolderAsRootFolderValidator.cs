using System;
using System.IO;
using FluentValidation;
using FluentValidation.Validators;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Organizer;

namespace Sonarr.Api.V3.Series
{
    public class SeriesFolderAsRootFolderValidator<T> : PropertyValidator<T, string>
    {
        private readonly IBuildFileNames _fileNameBuilder;

        public SeriesFolderAsRootFolderValidator(IBuildFileNames fileNameBuilder)
        {
            _fileNameBuilder = fileNameBuilder;
        }

        public override string Name => "SeriesFolderAsRootFolderValidator";

        protected override string GetDefaultMessageTemplate(string errorCode) => "Root folder path '{rootFolderPath}' contains series folder '{seriesFolder}'";

        public override bool IsValid(ValidationContext<T> context, string value)
        {
            if (value == null)
            {
                return true;
            }

            if (context.InstanceToValidate is not SeriesResource seriesResource)
            {
                return true;
            }

            if (value.IsNullOrWhiteSpace())
            {
                return true;
            }

            var rootFolder = new DirectoryInfo(value).Name;
            var series = seriesResource.ToModel();
            var seriesFolder = _fileNameBuilder.GetSeriesFolder(series);

            context.MessageFormatter.AppendArgument("rootFolderPath", value);
            context.MessageFormatter.AppendArgument("seriesFolder", seriesFolder);

            if (seriesFolder == rootFolder)
            {
                return false;
            }

            var distance = seriesFolder.LevenshteinDistance(rootFolder);

            return distance >= Math.Max(1, seriesFolder.Length * 0.2);
        }
    }
}
