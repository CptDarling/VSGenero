﻿/* ****************************************************************************
 * Copyright (c) 2015 Greg Fullman 
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution.
 * By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

using VSGenero.Navigation;
using Microsoft.VisualStudio.VSCommon;

using EnvDTE;
using EnvDTE80;

using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Navigation;
using Microsoft.VisualStudioTools.Project;
using VSGenero.EditorExtensions;
using System.IO;
using VSGenero.Options;
using VSGenero.Snippets;
#if DEV12_OR_LATER
using VSGenero.Peek;
#endif
using VSGenero.SqlSupport;
using System.Reflection;
using VSGenero.EditorExtensions.Intellisense;
using Microsoft.VisualStudio.Text.Adornments;
using VSGenero.Analysis;
using VSGenero.EditorExtensions.BraceCompletion;
using VSGenero.Analysis.Parsing.AST_4GL;

namespace VSGenero
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    // This attribute is used to register the informations needed to show the this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]

    [ProvideEditorExtensionAttribute(typeof(EditorFactory), VSGeneroConstants.FileExtension4GL, 32)]
    [ProvideEditorExtensionAttribute(typeof(EditorFactory), VSGeneroConstants.FileExtensionINC, 32)]
    [ProvideEditorExtensionAttribute(typeof(EditorFactory), VSGeneroConstants.FileExtensionPER, 32)]
    [ProvideEditorLogicalView(typeof(EditorFactory), "{7651a701-06e5-11d1-8ebd-00a0c90f26ea}")]
#if DEV12_OR_LATER
    [PeekSupportedContentType(".4gl")]
    [PeekSupportedContentType(".inc")]
#endif  
    [RegisterSnippetsAttribute(VSGeneroConstants.guidGenero4glLanguageService, false, 131, "Genero4GL", @"Snippets\CodeSnippets\SnippetsIndex.xml", @"Snippets\CodeSnippets\Snippets\", @"Snippets\CodeSnippets\Snippets\")]
    [ProvideLanguageService(typeof(VSGenero4GLLanguageInfo), VSGeneroConstants.LanguageName4GL, 106,
                            RequestStockColors = true,
                            ShowSmartIndent = true,       // enable this when we want to support smart indenting
                            ShowCompletion = true,
                            DefaultToInsertSpaces = true,
                            HideAdvancedMembersByDefault = true,
                            EnableAdvancedMembersOption = true,
                            ShowDropDownOptions = true)]
    [LanguageBraceCompletion(VSGeneroConstants.LanguageName4GL, EnableCompletion = true)]
    [ProvideLanguageExtension(typeof(VSGenero4GLLanguageInfo), VSGeneroConstants.FileExtension4GL)]
    [ProvideLanguageService(typeof(VSGeneroPERLanguageInfo), VSGeneroConstants.LanguageNamePER, 107,
                            RequestStockColors = true,
                            //ShowSmartIndent = true,       // enable this when we want to support smart indenting
                            ShowCompletion = true,
                            DefaultToInsertSpaces = true,
                            HideAdvancedMembersByDefault = true,
                            EnableAdvancedMembersOption = true,
                            ShowDropDownOptions = true)]
    [LanguageBraceCompletion(VSGeneroConstants.LanguageNamePER, EnableCompletion = true)]
    [ProvideLanguageExtension(typeof(VSGeneroPERLanguageInfo), VSGeneroConstants.FileExtensionPER)]
    [ProvideLanguageService(typeof(VSGeneroINCLanguageInfo), VSGeneroConstants.LanguageNameINC, 108,
                            RequestStockColors = true,
                            ShowSmartIndent = true,       // enable this when we want to support smart indenting
                            ShowCompletion = true,
                            DefaultToInsertSpaces = true,
                            HideAdvancedMembersByDefault = true,
                            EnableAdvancedMembersOption = true,
                            ShowDropDownOptions = true)]
    [LanguageBraceCompletion(VSGeneroConstants.LanguageNameINC, EnableCompletion = true)]
    [ProvideLanguageExtension(typeof(VSGeneroINCLanguageInfo), VSGeneroConstants.FileExtensionINC)]
    [ProvideLanguageEditorOptionPage(typeof(Genero4GLIntellisenseOptionsPage), VSGeneroConstants.LanguageName4GL, "", "Intellisense", "113")]
    [ProvideLanguageEditorOptionPage(typeof(Genero4GLAdvancedOptionsPage), VSGeneroConstants.LanguageName4GL, "", "Advanced", "114")]

    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]

#if DEV14_OR_LATER
    [ProvideAutoLoad(VSGeneroConstants.GeneroUiContextGuid)]
    [ProvideUIContextRule(VSGeneroConstants.GeneroUiContextGuid,
        name: "Genero auto-load",
        expression: "(Genero4glFile | GeneroPerFile | GeneroIncFile)",
        termNames: new[] { "Genero4glFile", "GeneroPerFile", "GeneroIncFile" },
        termValues: new[] { "ActiveEditorContentType:Genero4GL", "ActiveEditorContentType:GeneroPER", "ActiveEditorContentType:GeneroINC" })]
#else
    // This causes the package to autoload when Visual Studio starts (guid for UICONTEXT_NoSolution)
    [ProvideAutoLoad("{adfc4e64-0397-11d1-9f4e-00a0c911004f}")]
#endif

    [Guid(GuidList.guidVSGeneroPkgString)]
    public sealed class VSGeneroPackage : VSCommonPackage
    {
        private EditorFactory _generoEditorFactory;
        internal EditorFactory GeneroEditorFactory
        {
            get
            {
                if (_generoEditorFactory == null)
                    _generoEditorFactory = new EditorFactory(this);
                return _generoEditorFactory;
            }
        }

        private Genero4GLLanguagePreferences _langPrefs;
        internal Genero4GLLanguagePreferences LangPrefs
        {
            get
            {
                if(_langPrefs == null)
                {
                    InitializeLangPrefs();
                }
                return _langPrefs;
            }
        }

        private void InitializeLangPrefs()
        {
            IVsTextManager textMgr = (IVsTextManager)Instance.GetService(typeof(SVsTextManager));
            var langPrefs = new LANGPREFERENCES[1];
            langPrefs[0].guidLang = typeof(VSGenero4GLLanguageInfo).GUID;
            int result = textMgr.GetUserPreferences(null, null, langPrefs, null);
            _langPrefs = new Genero4GLLanguagePreferences(langPrefs[0]);

            Guid guid = typeof(IVsTextManagerEvents2).GUID;
            IConnectionPoint connectionPoint;
            ((IConnectionPointContainer)textMgr).FindConnectionPoint(ref guid, out connectionPoint);
            uint cookie;
            connectionPoint.Advise(_langPrefs, out cookie);
        }

        public new static VSGeneroPackage Instance;
        private GeneroProjectAnalyzer _analyzer;

        private Genero4GLIntellisenseOptions _intellisenseOptions4gl;
        public Genero4GLIntellisenseOptions IntellisenseOptions4GL
        {
            get
            {
                if (_intellisenseOptions4gl == null)
                    _intellisenseOptions4gl = new Genero4GLIntellisenseOptions();
                return _intellisenseOptions4gl;
            }
        }

        private Genero4GLAdvancedOptions _advancedOptions4gl;
        public Genero4GLAdvancedOptions AdvancedOptions4GL
        {
            get
            {
                if (_advancedOptions4gl == null)
                    _advancedOptions4gl = new Genero4GLAdvancedOptions();
                return _advancedOptions4gl;
            }
        }

        public VSGeneroPackage()
            : base()
        {
            Instance = this;
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }

        private List<string> _manuallyLoadedDlls = new List<string>();
        Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (string.IsNullOrEmpty(args.Name))
            {
                return null;
            }
            else
            {
                int index = args.Name.IndexOf(',');
                if (index < 0)
                    index = args.Name.Length;
                var assemblyName = args.Name.Substring(0, index) + ".dll";
                if (!_manuallyLoadedDlls.Contains(assemblyName))
                {
                    string asmLocation = Assembly.GetExecutingAssembly().Location;
                    asmLocation = Path.GetDirectoryName(asmLocation);
                    string filename = Path.Combine(asmLocation, assemblyName);

                    if (File.Exists(filename))
                    {
                        _manuallyLoadedDlls.Add(assemblyName);
                        return Assembly.LoadFrom(filename);
                    }
                }
            }
            return null;
        }

        /////////////////////////////////////////////////////////////////////////////
        // Overriden Package Implementation
        #region Package Members

        protected override string PackageName
        {
            get
            {
                return "VSGenero";
            }
        }

        protected override string PackageId
        {
            get
            {
                return VSGeneroConstants.VsixIdentity;
            }
        }

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initilaization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
            var services = (IServiceContainer)this;

            ThreadPool.QueueUserWorkItem(new WaitCallback(ContextCompletionMap.Instance.Initialize), null);

            var langService4GL = new VSGenero4GLLanguageInfo(this);
            services.AddService(langService4GL.GetType(), langService4GL, true);
            var langServicePER = new VSGeneroPERLanguageInfo(this);
            services.AddService(langServicePER.GetType(), langServicePER, true);

            services.AddService(
                typeof(ErrorTaskProvider),
                (container, serviceType) =>
                {
                    var errorList = GetService(typeof(SVsErrorList)) as IVsTaskList;
                    var model = ComponentModel;
                    var errorProvider = model != null ? model.GetService<IErrorProviderFactory>() : null;
                    return new ErrorTaskProvider(this, errorList, errorProvider);
                },
                promote: true);

            services.AddService(
                typeof(CommentTaskProvider),
                (container, serviceType) =>
                {
                    var taskList = GetService(typeof(SVsTaskList)) as IVsTaskList;
                    var model = ComponentModel;
                    var errorProvider = model != null ? model.GetService<IErrorProviderFactory>() : null;
                    return new CommentTaskProvider(this, taskList, errorProvider);
                },
                promote: true);

            InitializeLangPrefs();

            // TODO: not sure if this is needed...need to test
            DTE dte = (DTE)GetService(typeof(DTE));
            if (dte != null)
            {
                this.RegisterEditorFactory(GeneroEditorFactory);
            }

            RegisterCommands(new CommonCommand[]
                {
                    new ExtractSqlStatementsCommand()
                }, GuidList.guidVSGeneroCmdSet);

            var dte2 = (DTE2)Package.GetGlobalService(typeof(SDTE));
            var sp = new ServiceProvider(dte2 as Microsoft.VisualStudio.OLE.Interop.IServiceProvider);
        }

#endregion

        public override Type GetLibraryManagerType()
        {
            return typeof(IGeneroLibraryManager);
        }

        public override bool IsRecognizedFile(string filename)
        {
            var ext = Path.GetExtension(filename);

            return String.Equals(ext, VSGeneroConstants.FileExtension4GL, StringComparison.OrdinalIgnoreCase) ||
                String.Equals(ext, VSGeneroConstants.FileExtensionPER, StringComparison.OrdinalIgnoreCase);
        }

        private GeneroLibraryManager _generoLibManager;
        public override LibraryManager CreateLibraryManager(CommonPackage package)
        {
            if (_generoLibManager == null)
                _generoLibManager = new GeneroLibraryManager((VSGeneroPackage)package);
            return _generoLibManager;
        }

        public void LoadLibraryManager()
        {
            this.CreateService(null, typeof(IGeneroLibraryManager));
        }

        public object GetPackageService(Type t)
        {
            return Instance.GetService(t);
        }

        public Document ActiveDocument
        {
            get
            {
                EnvDTE.DTE dte = (EnvDTE.DTE)GetService(typeof(EnvDTE.DTE));
                return dte.ActiveDocument;
            }
        }

        /// <summary>
        /// The analyzer which is used for loose files.
        /// </summary>
        internal GeneroProjectAnalyzer DefaultAnalyzer
        {
            get
            {
                if (_analyzer == null)
                {
                    _analyzer = CreateAnalyzer();
                }
                return _analyzer;
            }
        }

        internal void RecreateAnalyzer()
        {
            if (_analyzer != null)
            {
                _analyzer.Dispose();
            }
            _analyzer = CreateAnalyzer();
        }

        private GeneroProjectAnalyzer CreateAnalyzer()
        {
            return new GeneroProjectAnalyzer(this, BuildTaskProvider, ErrorTaskProvider);
        }

        public IFunctionInformationProvider GlobalFunctionProvider { get; set; }

        public IDatabaseInformationProvider GlobalDatabaseProvider { get;  set;}

        public IEnumerable<ICommentValidator> CommentValidators { get; set; }

        public IBuildTaskProvider BuildTaskProvider { get; set; }


        private ErrorTaskProvider _errorTaskProvider;
        internal ErrorTaskProvider ErrorTaskProvider
        {
            get
            {
                if(_errorTaskProvider == null)
                {
                    _errorTaskProvider = (ErrorTaskProvider)this.GetService(typeof(ErrorTaskProvider));
                }
                return _errorTaskProvider;
            }
        }
        #region Program File Provider

        private object _programFileProviderLock = new object();
        private IProgramFileProvider _programFileProvider;
        public IProgramFileProvider ProgramFileProvider
        {
            get { return _programFileProvider; }
            set
            {
                lock (_programFileProviderLock)
                {
                    if (_programFileProvider == null)
                    {
                        _programFileProvider = value;
                        if (_programFileProvider != null)
                        {
                            _programFileProvider.ImportModuleLocationChanged += _programFileProvider_ImportModuleLocationChanged;
                            _programFileProvider.IncludeFileLocationChanged += _programFileProvider_IncludeFileLocationChanged;
                        }
                    }
                }
            }
        }

        void _programFileProvider_IncludeFileLocationChanged(object sender, IncludeFileLocationChangedEventArgs e)
        {
            DefaultAnalyzer.UpdateIncludedFile(e.NewLocation);
        }

        void _programFileProvider_ImportModuleLocationChanged(object sender, ImportModuleLocationChangedEventArgs e)
        {
            DefaultAnalyzer.UpdateImportedProject(e.ImportModule, e.NewLocation);
        }

#endregion

        private List<IContentType> _programCodeContentTypes;
        public IList<IContentType> ProgramCodeContentTypes
        {
            get
            {
                if (_programCodeContentTypes == null)
                    _programCodeContentTypes = new List<IContentType>();
                if(_programCodeContentTypes.Count == 0)
                {  
                    if (ComponentModel != null)
                    {
                        var regSvc = ComponentModel.GetService<IContentTypeRegistryService>();
                        if (regSvc != null)
                        {
                            _programCodeContentTypes.Add(regSvc.GetContentType(VSGeneroConstants.ContentType4GL));
                            _programCodeContentTypes.Add(regSvc.GetContentType(VSGeneroConstants.ContentTypeINC));
                            _programCodeContentTypes.Add(regSvc.GetContentType(VSGeneroConstants.ContentTypePER));
                        }
                    }
                }
                return _programCodeContentTypes;
            }
        }

        internal static ITextBuffer GetBufferForDocument(System.IServiceProvider serviceProvider, string filename)
        {
            IVsTextView viewAdapter;
            IVsWindowFrame frame;
            VsUtilities.OpenDocument(serviceProvider, filename, out viewAdapter, out frame);

            IVsTextLines lines;
            ErrorHandler.ThrowOnFailure(viewAdapter.GetBuffer(out lines));

            var adapter = serviceProvider.GetComponentModel().GetService<IVsEditorAdaptersFactoryService>();

            return adapter.GetDocumentBuffer(lines);
        }
    }
}
