﻿namespace AntlrVSIX.GoToDefintion
{
    using Antlr4.Runtime;
    using AntlrVSIX.Extensions;
    using AntlrVSIX.Grammar;
    using EnvDTE;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.TextManager.Interop;
    using System.Collections.Generic;
    using System.ComponentModel.Design;
    using System.IO;
    using System.Linq;
    using System;

    internal sealed class GoToDefinitionCommand
    {
        public const int _command_id = 0x0100;
        public static readonly Guid _command_set = new Guid("0c1acc31-15ac-417c-86b2-eefdc669e8bf");
        private readonly Package _package;
        private MenuCommand _menu_item;

        private GoToDefinitionCommand(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }
            _package = package;
            OleMenuCommandService commandService = this.ServiceProvider.GetService(
                typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(_command_set, _command_id);
                _menu_item = new MenuCommand(this.MenuItemCallback, menuCommandID);
                _menu_item.Enabled = false;
                _menu_item.Visible = false;
                commandService.AddCommand(_menu_item);
            }
        }

        public bool Enabled
        {
            set { _menu_item.Enabled = value; }
        }

        public bool Visible
        {
            set { _menu_item.Visible = value; }
        }

        public SnapshotSpan Symbol { get; set; }
        public string Classification { get; set; }
        public ITextView View { get; set; }
        public static GoToDefinitionCommand Instance { get; private set; }

        private IServiceProvider ServiceProvider
        {
            get { return this._package; }
        }

        public static void Initialize(Package package)
        {
            Instance = new GoToDefinitionCommand(package);
        }

        private void MenuItemCallback(object sender, EventArgs e)
        {
            ////////////////////////
            // Go to definition....
            ////////////////////////

            // First, open up every .g4 file in project.
            // Get VS solution, if any, and parse all grammars
            DTE application = DteExtensions.GetApplication();
            if (application != null)
            {
                IEnumerable<ProjectItem> iterator = DteExtensions.SolutionFiles(application);
                ProjectItem[] list = iterator.ToArray();
                foreach (var item in list)
                {
                    //var doc = item.Document; CRASHES!!!! DO NOT USE!
                    //var props = item.Properties;
                    string file_name = item.Name;
                    if (file_name != null)
                    {
                        string prefix = file_name.TrimSuffix(".g4");
                        if (prefix == file_name) continue;

                        try
                        {
                            object prop = item.Properties.Item("FullPath").Value;
                            string ffn = (string)prop;
                            if (!ParserDetails._per_file_parser_details.ContainsKey(ffn))
                            {
                                StreamReader sr = new StreamReader(ffn);
                                ParserDetails foo = new ParserDetails();
                                ParserDetails._per_file_parser_details[ffn] = foo;
                                foo.Parse(sr.ReadToEnd(), ffn);
                            }
                        } catch (Exception eeks)
                        { }
                    }
                }
            }

            string classification = this.Classification;
            SnapshotSpan span = this.Symbol;
            ITextView view = this.View;
            
            // First, find out what this view is, and what the file is.
            ITextBuffer buffer = view.TextBuffer;
            ITextDocument doc = buffer.GetTextDocument();
            string path = doc.FilePath;

            //ParserDetails details = null;
            //bool found = ParserDetails._per_file_parser_details.TryGetValue(path, out details);
            //if (!found) return;

            List<IToken> where = new List<IToken>();
            List<ParserDetails> where_details = new List<ParserDetails>();
            IToken token = null;
            foreach (var kvp in ParserDetails._per_file_parser_details)
            {
                string file_name = kvp.Key;
                ParserDetails details = kvp.Value;
                if (classification == AntlrVSIX.Constants.ClassificationNameNonterminal)
                {
                    var it = details._ant_nonterminals_defining.Where(
                        (t) => t.Text == span.GetText());
                    where.AddRange(it);
                    foreach (var i in it) where_details.Add(details);
                }
                else if (classification == AntlrVSIX.Constants.ClassificationNameTerminal)
                {
                    var it = details._ant_terminals_defining.Where(
                        (t) => t.Text == span.GetText());
                    where.AddRange(it);
                    foreach (var i in it) where_details.Add(details);
                }
            }
            if (where.Any()) token = where.First();
            else return;
            ParserDetails where_token = where_details.First();

            string full_file_name = where_token.full_file_name;
            IVsTextView vstv = IVsTextViewExtensions.GetIVsTextView(full_file_name);
            IVsTextViewExtensions.ShowFrame(full_file_name);
            vstv = IVsTextViewExtensions.GetIVsTextView(where_token.full_file_name);

            IWpfTextView wpftv = vstv.GetIWpfTextView();
            if (wpftv == null) return;

            int line_number;
            int colum_number;
            vstv.GetLineAndColumn(token.StartIndex, out line_number, out colum_number);

            // Create new span in the appropriate view.
            ITextSnapshot cc = wpftv.TextBuffer.CurrentSnapshot;
            SnapshotSpan ss = new SnapshotSpan(cc, token.StartIndex, 1);
            SnapshotPoint sp = ss.Start;
            // Put cursor on symbol.
            wpftv.Caret.MoveTo(sp);     // This sets cursor, bot does not center.
            // Center on cursor.
            //wpftv.Caret.EnsureVisible(); // This works, sort of. It moves the scroll bar, but it does not CENTER! Does not really work!
            if (line_number > 0)
                vstv.CenterLines(line_number - 1, 2);
            else
                vstv.CenterLines(line_number, 1);
        }
    }
}
