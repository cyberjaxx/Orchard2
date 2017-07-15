using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.AspNetCore.Mvc.Razor.Internal;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Orchard.Mvc
{
    public class SharedViewCompilerProvider : IViewCompilerProvider
    {
        private static IViewCompiler _compiler;

        private readonly RazorTemplateEngine _razorTemplateEngine;
        private readonly ApplicationPartManager _applicationPartManager;
        private readonly IRazorViewEngineFileProviderAccessor _fileProviderAccessor;
        private readonly CSharpCompiler _csharpCompiler;
        private readonly RazorViewEngineOptions _viewEngineOptions;
        private readonly ILogger<RazorViewCompiler> _logger;
        private readonly Func<IViewCompiler> _createCompiler;

        private readonly IEnumerable<IApplicationFeatureProvider<ViewsFeature>> _viewsFeatureProviders;
        private readonly IHostingEnvironment _hostingEnvironment;

        private object _initializeLock = new object();
        private bool _initialized;

        public SharedViewCompilerProvider(
            ApplicationPartManager applicationPartManager,
            RazorTemplateEngine razorTemplateEngine,
            IRazorViewEngineFileProviderAccessor fileProviderAccessor,
            CSharpCompiler csharpCompiler,
            IOptions<RazorViewEngineOptions> viewEngineOptionsAccessor,
            ILoggerFactory loggerFactory,
            IEnumerable<IApplicationFeatureProvider<ViewsFeature>> viewsFeatureProviders,
            IHostingEnvironment hostingEnvironment)
        {
            _applicationPartManager = applicationPartManager;
            _razorTemplateEngine = razorTemplateEngine;
            _fileProviderAccessor = fileProviderAccessor;
            _csharpCompiler = csharpCompiler;
            _viewEngineOptions = viewEngineOptionsAccessor.Value;

            _logger = loggerFactory.CreateLogger<RazorViewCompiler>();
            _createCompiler = CreateCompiler;

            _viewsFeatureProviders = viewsFeatureProviders;
            _hostingEnvironment = hostingEnvironment;
        }

        public IViewCompiler GetCompiler()
        {
            var fileProvider = _fileProviderAccessor.FileProvider;
            if (fileProvider is NullFileProvider)
            {
                var message = string.Format(CultureInfo.CurrentCulture,
                    "'{0}.{1}' must not be empty. At least one '{2}' is required to locate a view for rendering.",
                    typeof(RazorViewEngineOptions).FullName,
                    nameof(RazorViewEngineOptions.FileProviders),
                    typeof(IFileProvider).FullName);
                throw new InvalidOperationException(message);
            }

            return LazyInitializer.EnsureInitialized(
                ref _compiler,
                ref _initialized,
                ref _initializeLock,
                _createCompiler);
        }

        private IViewCompiler CreateCompiler()
        {
            if (_compiler == null)
            {
                var feature = new ViewsFeature();

                var featureProviders = _applicationPartManager.FeatureProviders
                    .OfType<IApplicationFeatureProvider<ViewsFeature>>()
                    .ToList();

                featureProviders.AddRange(_viewsFeatureProviders);

                var assemblyParts =
                    new AssemblyPart[]
                    {
                        new AssemblyPart(Assembly.Load(new AssemblyName(_hostingEnvironment.ApplicationName)))
                    };

                foreach (var provider in featureProviders)
                {
                    provider.PopulateFeature(assemblyParts, feature);
                }

                _compiler = new RazorViewCompiler(
                _fileProviderAccessor.FileProvider,
                _razorTemplateEngine,
                _csharpCompiler,
                _viewEngineOptions.CompilationCallback,
                feature.ViewDescriptors,
                _logger);
            }

            return _compiler;
        }
    }
}