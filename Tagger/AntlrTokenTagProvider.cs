﻿namespace AntlrVSIX.Tagger
{
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Utilities;
    using System.ComponentModel.Composition;

    [Export(typeof(ITaggerProvider))]
    [ContentType(Constants.ContentType)]
    [TagType(typeof(AntlrTokenTag))]
    internal sealed class AntlrTokenTagProvider : ITaggerProvider
    {
        [Import]
        SVsServiceProvider GlobalServiceProvider = null;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return new AntlrTokenTagger(buffer, GlobalServiceProvider) as ITagger<T>;
        }
    }
}
