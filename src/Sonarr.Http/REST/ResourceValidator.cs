using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using FluentValidation;
using Sonarr.Http.ClientSchema;

namespace Sonarr.Http.REST
{
    public class ResourceValidator<TResource> : AbstractValidator<TResource>
    {
        public IRuleBuilderInitial<TResource, TProperty> RuleForField<TProperty>(Expression<Func<TResource, IEnumerable<Field>>> fieldListAccessor, string fieldName)
        {
            var accessor = fieldListAccessor.Compile();
            var builder = RuleFor(resource => (TProperty)GetValue(resource, accessor, fieldName));

            // The value is computed from a field collection rather than a real member, so the
            // property name has to be set explicitly for validation failures to map to the field.
            ((IRuleBuilderOptions<TResource, TProperty>)builder).OverridePropertyName(fieldName);

            return builder;
        }

        private static object GetValue(TResource container, Func<TResource, IEnumerable<Field>> fieldListAccessor, string fieldName)
        {
            var resource = fieldListAccessor(container).SingleOrDefault(c => c.Name == fieldName);

            return resource?.Value;
        }
    }
}
