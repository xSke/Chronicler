using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SIBR.Storage.API.Utils
{
    public class CommaSeparatedBinder: IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (valueProviderResult.Length == 0)
                return Task.CompletedTask;

            var value = valueProviderResult.FirstValue;

            var elemType = bindingContext.ModelType.GetElementType();
            var tc = TypeDescriptor.GetConverter(elemType!);

            var strings = value.Split(",").ToArray();
            var output = Array.CreateInstance(elemType, strings.Length);
            for (var i = 0; i < strings.Length; i++) 
                output.SetValue(tc.ConvertFrom(strings[i]), i);

            bindingContext.Result = ModelBindingResult.Success(output);
            return Task.CompletedTask;
        }
    }
}