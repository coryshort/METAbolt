//  Copyright (c) 2008-2011, www.metabolt.net
//  All rights reserved.

//  Redistribution and use in source and binary forms, with or without modification, 
//  are permitted provided that the following conditions are met:

//  * Redistributions of source code must retain the above copyright notice, 
//    this list of conditions and the following disclaimer. 
//  * Redistributions in binary form must reproduce the above copyright notice, 
//    this list of conditions and the following disclaimer in the documentation 
//    and/or other materials provided with the distribution. 
//  * Neither the name METAbolt nor the names of its contributors may be used to 
//    endorse or promote products derived from this software without specific prior 
//    written permission. 

//  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND 
//  ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED 
//  WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. 
//  IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, 
//  INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT 
//  NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR 
//  PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
//  WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) 
//  ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE 
//  POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using SLNetworkComm;
using OpenMetaverse;
using OpenMetaverse.Assets;
using ScintillaNet;
using System.IO;
using System.Diagnostics;
using System.Globalization;


namespace METAbolt
{
    public partial class frmScriptEditor : Form
    {
        private METAboltInstance instance;
        private SLNetCom netcom;
        private GridClient client;
        private InventoryItem item;
        //private UUID transferID;
        //private AssetScriptText receivedAsset;

        //private UUID uploadID;
        private bool closePending = false;
        private bool saving = false;
        //private bool changed = false;
        //private List<string> acomplete;
        private bool ointernal = false;
        private bool istaskobj = false;
        private UUID objectid = UUID.Zero;  

        //int start = 0;
        //int indexOfSearchText = 0;
        //string prevsearchtxt = string.Empty;
        private const int LINE_NUMBERS_MARGIN_WIDTH = 35;
        private UUID assetUUID = UUID.Zero;
        private UUID itemUUID = UUID.Zero;  

        public frmScriptEditor(METAboltInstance instance, InventoryItem item)
        {
            InitializeComponent();

            this.instance = instance;
            netcom = this.instance.Netcom;
            client = this.instance.Client;
            this.item = item;
            istaskobj = false;
            panel1.Visible = false;  

            Disposed += new EventHandler(Script_Disposed);

            AddNetcomEvents();
            
            this.Text = item.Name + " (script) - METAbolt";

            SetScintilla();

            assetUUID = item.AssetUUID;
            client.Assets.RequestInventoryAsset(assetUUID, item.UUID, UUID.Zero, item.OwnerID, item.AssetType, true, Assets_OnAssetReceived);
        }

        public frmScriptEditor(METAboltInstance instance, InventoryLSL item, Primitive obj)
        {
            InitializeComponent();

            this.instance = instance;
            netcom = this.instance.Netcom;
            client = this.instance.Client;
            this.item = item;
            istaskobj = true;
            panel1.Visible = true;

            AddNetcomEvents();

            this.Text = item.Name + " (script) - METAbolt";

            SetScintilla();

            assetUUID = item.AssetUUID;
            objectid = obj.ID;
            itemUUID = item.UUID; 

            client.Assets.RequestInventoryAsset(assetUUID, item.UUID, obj.ID, obj.OwnerID, item.AssetType, true, Assets_OnAssetReceived);
        }

        public frmScriptEditor(METAboltInstance instance)
        {
            InitializeComponent();

            this.instance = instance;
            netcom = this.instance.Netcom;
            client = this.instance.Client;
            panel1.Visible = false;

            AddNetcomEvents();

            PB1.Visible = false;
            tsStatus.Text = "Ready.";

            SetScintilla();
        }

        private void SetScintilla()
        {
            try
            {
                rtbScript.Margins.Margin0.Width = LINE_NUMBERS_MARGIN_WIDTH;
                rtbScript.AutoComplete.FillUpCharacters = ".([";
                rtbScript.AutoComplete.AutomaticLengthEntered = true;
                rtbScript.AutoComplete.AutoHide = true;
                rtbScript.AutoComplete.IsCaseSensitive = false;
                rtbScript.AutoComplete.RegisterImages(imageList1);
                //rtbScript.Styles[rtbScript.Lexing.StyleNameMap["Keyword"]].ForeColor = Color.Fuchsia;

                rtbScript.Caret.Color = Color.Red;

                rtbScript.AutoComplete.SingleLineAccept = false;
                rtbScript.AutoComplete.DropRestOfWord = true;
                rtbScript.AutoComplete.StopCharacters = ") ] // ; ' , - _ */ /*";
                SetLanguage("lsl");
                tscboLanguage.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                ex.Message,
                "METAbolt",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            }
        }

        private void SetupSort()
        {
            AutoCompleteStringListSorter Sorter = new AutoCompleteStringListSorter();
            Sorter.SortingOrder = SortOrder.Ascending;
            rtbScript.AutoComplete.List.Sort(Sorter);
            rtbScript.CharAdded += new EventHandler<CharAddedEventArgs>(Document_CharAdded);
        }

        private void Document_CharAdded(object sender, CharAddedEventArgs e)
        {
            if (e.Ch == '(')
            {
                //rtbScript.CallTip.Show("this is a calltip");
            }
            else if (e.Ch == '\r' || e.Ch == '\n')
            {
                // do nothing
            }
            else
            {
                rtbScript.CallTip.Hide();

                if (e.Ch != ' ' && e.Ch != '/') // autocomplete on dot
                {
                    if (rtbScript.AutoComplete.List.Count > 0)
                    {
                        rtbScript.AutoComplete.Show();
                    }
                }
            }
        }

        public static void Append<T>(ref T[] array, params T[] items)
        {
            int oldLength = array.Length;
            //make room for new items
            Array.Resize<T>(ref array, oldLength + items.Length);

            for (int i = 0; i < items.Length; i++)
                array[oldLength + i] = items[i];
        }

        private void PrepareList()
        {
            //// TODO: When this form becomes a proper IDE all this
            //// needs to be moved to the IDE level so that it is
            //// not loaded each time a script is opened. LL

            //char[] deli = " ".ToCharArray();
            //string[] strKeys0 = rtbScript.Lexing.Keywords[0].Split(deli);
            //string[] strKeys1 = rtbScript.Lexing.Keywords[1].Split(deli);
            //string[] strKeys3 = rtbScript.Lexing.Keywords[3].Split(deli);
            ////string[] strKeys4 = {" "};

            //ArrayX.Append<string>(ref strKeys0, strKeys1);
            //ArrayX.Append<string>(ref strKeys0, strKeys3);
            ////ArrayX.Append<string>(ref strKeys0, strKeys4);

            //acomplete = new List<string>(strKeys0);
            //rtbScript.AutoComplete.List = acomplete;
        }

        //Separate thread
        private void Assets_OnAssetReceived(AssetDownload transfer, Asset asset)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate()
                {
                    Assets_OnAssetReceived(transfer, asset);
                }
                ));

                return;
            }

            if (transfer.AssetType != AssetType.LSLText) return;

            try
            {
                string scriptContent;

                if (!transfer.Success)
                {
                    scriptContent = "Unable to download script. Make sure you have the proper permissions!";
                    SetScriptText(scriptContent, false);
                    return;
                }

                //receivedAsset = (AssetScriptText)asset;
                scriptContent = Utils.BytesToString(transfer.AssetData);
                SetScriptText(scriptContent, false);
                //string adta = string.Empty; 

                if (istaskobj)
                {
                    client.Inventory.ScriptRunningReply += new EventHandler<ScriptRunningReplyEventArgs>(Inventory_ScriptRunningReply);
                    client.Inventory.RequestGetScriptRunning(objectid, itemUUID);   //assetUUID);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                ex.Message,
                "METAbolt",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            }
        }

        private void Inventory_ScriptRunningReply(object sender, ScriptRunningReplyEventArgs e)
        {
            BeginInvoke(new MethodInvoker(delegate()
            {
                checkBox1.Checked = e.IsMono;
                checkBox2.Checked = e.IsRunning;
            }));
             
            client.Inventory.ScriptRunningReply -= new EventHandler<ScriptRunningReplyEventArgs>(Inventory_ScriptRunningReply);  
        }

        //UI thread
        private delegate void OnSetScriptText(string text, bool readOnly);
        private void SetScriptText(string text, bool readOnly)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate()
                {
                    SetScriptText(text, readOnly);
                }));

                return;
            }

            try
            {
                rtbScript.UndoRedo.EmptyUndoBuffer();
                rtbScript.Modified = false;
                rtbScript.Text = text;

                if (readOnly)
                {
                    rtbScript.IsReadOnly = true;
                    rtbScript.BackColor = Color.FromKnownColor(KnownColor.Control);
                }
                else
                {
                    rtbScript.IsReadOnly = false;
                    rtbScript.BackColor = Color.White;
                }

                //rtbScript.ProcessAllLines();
                PB1.Visible = false;
                tsStatus.Text = "Ready.";

                if (!rtbScript.IsReadOnly)
                {
                    if (!ointernal)
                    {
                        tsSave.Enabled = true;
                        tsbSave.Enabled = true; 
                    }

                    ointernal = false;
                    tsSaveDisk.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                ex.Message,
                "METAbolt",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            }
        }

        private void AddNetcomEvents()
        {
            netcom.ClientLoggedOut += new EventHandler(netcom_ClientLoggedOut);
        }

        private void Script_Disposed(object sender, EventArgs e)
        {
            netcom.ClientLoggedOut -= new EventHandler(netcom_ClientLoggedOut);
            rtbScript.CharAdded -= new EventHandler<CharAddedEventArgs>(Document_CharAdded);
        }

        private void netcom_ClientLoggedOut(object sender, EventArgs e)
        {
            closePending = false;
            this.Close();
        }

        private DialogResult AskForSave()
        {
            return MessageBox.Show(
                "Your changes have not been saved. Save the script?",
                "METAbolt",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

        }

        private void SaveScript()
        {
            try
            {
                PB1.Visible = true;
                saving = true;

                rtbScript.IsReadOnly = true;
                rtbScript.BackColor = Color.FromKnownColor(KnownColor.Control);

                tsStatus.Text = "Saving script...";
                tsStatus.Visible = true;
                tsSave.Enabled = false;
                tsbSave.Enabled = false;
                tsSaveDisk.Enabled = false;

                //string file = this.item.Name;
                //string desc = this.item.Description;

                if (istaskobj)
                {
                    client.Inventory.RequestUpdateScriptTask(CreateScriptAsset(rtbScript.Text), this.item.UUID, objectid, checkBox1.Checked, checkBox2.Checked, new InventoryManager.ScriptUpdatedCallback(OnScriptUpdate));
                }
                else
                {
                    client.Inventory.RequestUpdateScriptAgentInventory(CreateScriptAsset(rtbScript.Text), this.item.UUID, true, new InventoryManager.ScriptUpdatedCallback(OnScriptUpdate));
                }

                //changed = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                ex.Message,
                "METAbolt",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            }
        }

        private byte[] CreateScriptAsset(string body)
        {
            try
            {
                body = body.Trim();

                // Format the string body into Linden text
                string lindenText = body;

                // Assume this is a string, add 1 for the null terminator
                byte[] stringBytes = System.Text.Encoding.UTF8.GetBytes(lindenText);
                byte[] assetData = new byte[stringBytes.Length]; //+ 1];
                Array.Copy(stringBytes, 0, assetData, 0, stringBytes.Length);
                return assetData;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                ex.Message,
                "METAbolt",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            }

            return null;
        }

        void OnScriptUpdate(bool success, string status, bool compile, List<string> messages, UUID itemID, UUID assetID)
        {
            if (success)
            {
                saving = false;
                BeginInvoke(new MethodInvoker(SaveComplete));

                if (closePending)
                {
                    closePending = false;
                    this.Close();
                    return;
                }
            }    
        }

        //UI thread
        private void SaveComplete()
        {
            rtbScript.IsReadOnly = false;
            rtbScript.BackColor = Color.White;
            tsSave.Enabled = false;
            tsbSave.Enabled = false;
            tsSaveDisk.Enabled = true;
            rtbScript.Modified = false;
            PB1.Visible = false;

            tsStatus.Text = "Save completed.";
            //lblSaveStatus.Visible = true;
        }

        private void frmScriptEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (closePending || saving)
                e.Cancel = true;
            else if (rtbScript.Modified)
            {
                if (!ointernal)
                {
                    DialogResult result = AskForSave();

                    switch (result)
                    {
                        case DialogResult.Yes:
                            closePending = true;
                            SaveScript();

                            if (saving)
                            {
                                e.Cancel = true;
                            }
                            else
                            {
                                e.Cancel = false;
                            }
                            break;

                        case DialogResult.No:
                            e.Cancel = false;
                            break;

                        case DialogResult.Cancel:
                            e.Cancel = true;
                            break;
                    }
                }
            }
        }

        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtbScript.Clipboard.Cut();
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Clipboard.Clear();
            rtbScript.Clipboard.Copy();
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //if (Clipboard.ContainsText(TextDataFormat.Rtf)) 
            //{
                
            //}

            rtbScript.Clipboard.Paste();
        }

        private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtbScript.Selection.SelectAll(); 
        }       

        private void SaveToDisk()
        {
            // Create a SaveFileDialog to request a path and file name to save to.
            SaveFileDialog saveFile1 = new SaveFileDialog();

            string logdir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\METAbolt";

            saveFile1.InitialDirectory = logdir;

            // Initialize the SaveFileDialog to specify the RTF extension for the file.
            saveFile1.DefaultExt = "*.lsl";
            saveFile1.Filter = "LSL Files (*.lsl)|*.lsl|C# Class (*.cs)|*.cs|XML Files (*.xml)|*.xml|HTML Files (*.html)|*.html|Java Script (*.js)|*.js|VB Script (*.vb)|*.vb|PHP Files (*.php)|*.php|INI Files (*.ini)|*.ini|AIML Files (*.aiml)|*.aiml|TXT Files (*.txt)|*.txt|RTF Files (*.rtf)|*.rtf|All Files (*.*)|*.*";  //"RTF Files|*.rtf";
            saveFile1.Title = "Save to hard disk...";

            // Determine if the user selected a file name from the saveFileDialog.
            if (saveFile1.ShowDialog() == System.Windows.Forms.DialogResult.OK &&
               saveFile1.FileName.Length > 0)
            {
                using (FileStream fs = File.Create(saveFile1.FileName))
                using (BinaryWriter bw = new BinaryWriter(fs))
                    bw.Write(rtbScript.RawText, 0, rtbScript.RawText.Length - 1); // Omit trailing NULL

                rtbScript.Modified = false;

                tsSaveDisk.Enabled = false;

                //if (saveFile1.FileName.Substring(saveFile1.FileName.Length - 3) == "rtf")
                //{
                //    // Save the contents of the RichTextBox into the file.
                //    //rtbScript.SaveFile(saveFile1.FileName, RichTextBoxStreamType.RichText);
                //    using (FileStream fs = File.Create(saveFile1.InitialDirectory + "\\" + saveFile1.FileName))
                //    using (BinaryWriter bw = new BinaryWriter(fs))
                //        bw.Write(rtbScript.RawText, 0, rtbScript.RawText.Length - 1); // Omit trailing NULL

                //    rtbScript.Modified = false;
                //}
                //else
                //{
                //    rtbScript.SaveFile(saveFile1.FileName, RichTextBoxStreamType.PlainText);
                //}
            }

            saveFile1.Dispose();  
        }

        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtbScript.UndoRedo.Undo();
        }

        private void redoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtbScript.UndoRedo.Redo(); 
        }

        private void GetCurrentLine()
        {
            //int linenumber = rtbScript.GetLineFromCharIndex(rtbScript.SelectionStart) + 1;
            //tsLn.Text = "Ln " + linenumber.ToString();
            Line ln = rtbScript.Lines.Current;
            int lnm = ln.Number + 1;
            tsLn.Text = "Ln " + lnm.ToString(CultureInfo.CurrentCulture);
        }

        private void GetCurrentCol()
        {
            //int colnumber = rtbScript.SelectionStart - rtbScript.GetFirstCharIndexOfCurrentLine() + 1;
            //tsCol.Text = "Ln " + colnumber.ToString();

            //int colnumber = rtbScript.CurrentPos;
            tsCol.Text = "Col " + rtbScript.GetColumn(rtbScript.CurrentPos).ToString(CultureInfo.CurrentCulture);
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close(); 
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            toolStrip2.Visible = false; 
        }

        private void tsSaveDisk_Click(object sender, EventArgs e)
        {
            SaveToDisk();
        }

        private void tsSave_Click(object sender, EventArgs e)
        {
            SaveScript();
        }

        //public bool IniLexer
        //{
        //    get { return _iniLexer; }
        //    set { _iniLexer = value; }
        //}

        private void SetLanguage(string language)
        {
            if ("ini".Equals(language, StringComparison.OrdinalIgnoreCase))
            {
                // Reset/set all styles and prepare scintilla for custom lexing
                //IniLexer = true;
                IniLexer.Init(rtbScript);
            }
            else
            {
                // Use a built-in lexer and configuration
                //ActiveDocument.IniLexer = false;
                rtbScript.ConfigurationManager.Language = language;

                // Smart indenting...
                if ("cs".Equals(language, StringComparison.OrdinalIgnoreCase))
                {
                    rtbScript.Indentation.SmartIndentType = SmartIndent.CPP;
                }
                else if ("lsl".Equals(language, StringComparison.OrdinalIgnoreCase))
                {
                    rtbScript.Indentation.SmartIndentType = SmartIndent.CPP;
                }
                else if ("js".Equals(language, StringComparison.OrdinalIgnoreCase))
                {
                    rtbScript.Indentation.SmartIndentType = SmartIndent.CPP;
                }
                else
                {
                    rtbScript.Indentation.SmartIndentType = SmartIndent.Simple;
                }
            }

            SetupSort();
        }

        private void frmScriptEditor_Load(object sender, EventArgs e)
        {
            this.CenterToParent();
        }

        private void goToToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtbScript.GoTo.ShowGoToDialog();
        }

        private void findToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtbScript.FindReplace.ShowFind(); 
        }

        private void replaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtbScript.FindReplace.ShowReplace(); 
        }

        private void advancedToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void previousBookmarkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Line l = rtbScript.Lines.Current.FindPreviousMarker(1);
            if (l != null)
                l.Goto();
        }

        private void toggleBookmarkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Line currentLine = rtbScript.Lines.Current;
            if (rtbScript.Markers.GetMarkerMask(currentLine) == 0)
			{
				currentLine.AddMarker(0);
			}
			else
			{
				currentLine.DeleteMarker(0);
			}
        }

        private void nextBookmarkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Line l = rtbScript.Lines.Current.FindNextMarker(1);
            if (l != null)
                l.Goto();
        }

        private void clearBookmarksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtbScript.Markers.DeleteAll(0);
        }

        private void dropToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtbScript.DropMarkers.Drop();
        }

        private void collectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtbScript.DropMarkers.Collect();
        }

        private void insertSnippetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtbScript.Snippets.ShowSnippetList();
        }

        private void makeUpperCaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtbScript.Commands.Execute(BindableCommand.UpperCase);
        }

        private void makeLowerCaseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtbScript.Commands.Execute(BindableCommand.LowerCase);
        }

        private void commentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtbScript.Commands.Execute(BindableCommand.LineComment);
        }

        private void uncommentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtbScript.Commands.Execute(BindableCommand.LineUncomment);
        }

        private void contectHelpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (tscboLanguage.SelectedItem.ToString().ToLower(CultureInfo.CurrentCulture) == "lsl")
            {
                string hword = rtbScript.GetWordFromPosition(rtbScript.CurrentPos);
                string surl = "http://wiki.secondlife.com/wiki/" + hword;
                System.Diagnostics.Process.Start(@surl);
            }
        }

        private void tscboLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            string lang = string.Empty;

            rtbScript.AutoComplete.List = null;

            switch (tscboLanguage.SelectedItem.ToString().ToLower(CultureInfo.CurrentCulture))
            {
                case "c#":
                    lang = "cs";
                    break;

                case "html":
                    lang = "html";
                    break;

                case "sql":
                    lang = "mssql";
                    break;

                case "vbscript":
                    lang = "vbscript";
                    break;

                case "xml":
                    lang = "xml";
                    break;

                case "java":
                    lang = "js";
                    break;

                case "lsl":
                    lang = "lsl";
                    break;

                case "php":
                    lang = "php";
                    break;

                case "ini":
                    lang = "ini";
                    break;

                case "aiml":
                    lang = "xml";
                    break;

                case "text":
                    lang = "default";
                    break;

                case "bat/cmd":
                    lang = "bat";
                    break;
            }

            SetLanguage(lang);

            //if (lang == "lsl")
            //{
            //    PrepareList();
            //}
        }

        private void tscboLanguage_Click(object sender, EventArgs e)
        {

        }

        private void toolStripButton2_Click_1(object sender, EventArgs e)
        {
            rtbScript.Commands.Execute(BindableCommand.LineComment);
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            rtbScript.Commands.Execute(BindableCommand.LineUncomment);
        }

        private void toolStripButton9_Click(object sender, EventArgs e)
        {
            Line l = rtbScript.Lines.Current.FindPreviousMarker(1);
            if (l != null)
                l.Goto();
        }

        private void toolStripButton8_Click(object sender, EventArgs e)
        {
            Line l = rtbScript.Lines.Current.FindNextMarker(1);
            if (l != null)
                l.Goto();
        }

        private void toolStripButton10_Click(object sender, EventArgs e)
        {
            rtbScript.Snippets.ShowSnippetList();
        }

        private void toolStripButton6_Click(object sender, EventArgs e)
        {
            rtbScript.Commands.Execute(BindableCommand.BackTab);
        }

        private void toolStripButton7_Click(object sender, EventArgs e)
        {
            rtbScript.Commands.Execute(BindableCommand.Tab);
        }

        private void whitespaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (whitespaceToolStripMenuItem.Checked)
            {
                rtbScript.Whitespace.Mode = WhitespaceMode.VisibleAlways;
            }
            else
            {
                rtbScript.Whitespace.Mode = WhitespaceMode.Invisible;
            }
        }

        private void wordWrapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (wordWrapToolStripMenuItem.Checked)
            {
                rtbScript.LineWrap.Mode = WrapMode.Word;
            }
            else
            {
                rtbScript.LineWrap.Mode = WrapMode.None;
            }
        }

        private void endOfLineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (endOfLineToolStripMenuItem.Checked)
            {

                rtbScript.EndOfLine.IsVisible = true;
            }
            else
            {
                rtbScript.EndOfLine.IsVisible = false;
            }
        }

        private void lineNumbersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //lineNumbersToolStripMenuItem.Checked = !lineNumbersToolStripMenuItem.Checked;

            if (lineNumbersToolStripMenuItem.Checked)
            {
                rtbScript.Margins.Margin0.Width = LINE_NUMBERS_MARGIN_WIDTH;
            }
            else
            {
                rtbScript.Margins.Margin0.Width = 0;
            }
        }

        private void foldLevelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtbScript.Lines.Current.FoldExpanded = true;
            rtbScript.Refresh();
        }

        private void unfoldLevelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtbScript.Lines.Current.FoldExpanded = false;
            rtbScript.Refresh();
        }

        private void foldAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Line l in rtbScript.Lines)
            {
                l.FoldExpanded = true;
            }

            rtbScript.Refresh();  
        }

        private void unfoldAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Line l in rtbScript.Lines)
            {
                l.FoldExpanded = false;
            }

            rtbScript.Refresh();
        }

        private void navigateForwardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtbScript.DocumentNavigation.NavigateForward();
        }

        private void navigateBackwardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtbScript.DocumentNavigation.NavigateBackward();
        }

        private void tsLn_DoubleClick(object sender, EventArgs e)
        {
            rtbScript.GoTo.ShowGoToDialog();
        }

        private void tsCol_DoubleClick(object sender, EventArgs e)
        {
            rtbScript.GoTo.ShowGoToDialog();
        }

        private void toolStripButton12_Click(object sender, EventArgs e)
        {
            SaveToDisk();
        }

        private void toolStripButton13_Click(object sender, EventArgs e)
        {
            rtbScript.Clipboard.Cut();
        }

        private void toolStripButton14_Click(object sender, EventArgs e)
        {
            rtbScript.Clipboard.Copy();
        }

        private void toolStripButton15_Click(object sender, EventArgs e)
        {
            rtbScript.Clipboard.Paste();
        }

        private void toolStripButton16_Click(object sender, EventArgs e)
        {
            rtbScript.UndoRedo.Undo();
        }

        private void toolStripButton17_Click(object sender, EventArgs e)
        {
            rtbScript.UndoRedo.Redo();
        }

        private void lSLFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //tsSaveDisk.Enabled = true;
            //tsSave.Enabled = false;
            ointernal = true;

            string lslb = @"
default
{
    state_entry()
    {
        llSay(0,'Hello METAbolt user');
     }
}";

            rtbScript.Text = lslb;
            SetLanguage("lsl");
            tscboLanguage.SelectedIndex = 0;
        }

        private void cClassToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tsSaveDisk.Enabled = true;

            string csb = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using METAbolt;  // don't forget to add METAbolt as a reference

namespace METAbolt
{
    class Class1
    {
    }
}";

            rtbScript.Text = csb;
            SetLanguage("c#");
            tscboLanguage.SelectedIndex = 1;
        }

        private void xMLFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tsSaveDisk.Enabled = true;

            string xmlb = @"
<?xml version=""1.0""?>
    <Level1>
      <level2>
      </level2>
    </Level1>";

            rtbScript.Text = xmlb;
            SetLanguage("xml");
            tscboLanguage.SelectedIndex = 6;
        }

        private void hTNLFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tsSaveDisk.Enabled = true;

            string htmlb = @"
<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.0 Transitional//EN"" ""http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd"">
<html xmlns=""http://www.w3.org/1999/xhtml"">

<head>
<meta content=""text/html; charset=utf-8"" http-equiv=""Content-Type"" />
<title>Untitled 1</title>
</head>

<body>

</body>

</html>";

            rtbScript.Text = htmlb;
            SetLanguage("html");
            tscboLanguage.SelectedIndex = 7;
        }

        private void textFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rtbScript.Text = string.Empty;
            SetLanguage("text");
            tscboLanguage.SelectedIndex = 10;
        }

        private void toolStripButton11_Click(object sender, EventArgs e)
        {
            openFileDialog.Filter = "LSL Files (*.lsl)|*.lsl|C# Class (*.cs)|*.cs|XML Files (*.xml)|*.xml|HTML Files (*.html)|*.html|Java Script (*.js)|*.js|VB Script (*.vb)|*.vb|PHP Files (*.php)|*.php|INI Files (*.ini)|*.ini|AIML Files (*.aiml)|*.aiml|TXT Files (*.txt)|*.txt|RTF Files (*.rtf)|*.rtf|All Files (*.*)|*.*";  //"RTF Files|*.rtf"; 
            OpenFile();
        }

        private void OpenFile()
        {
            if (openFileDialog.ShowDialog() != DialogResult.OK)
                return;

            int ind = openFileDialog.FilterIndex;
            openFileDialog.FilterIndex = 1;
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\METAbolt";

            OpenFile(openFileDialog.FileName, ind);
        }

        private void OpenFile(string filePath, int ind)
        {
            rtbScript.Text = File.ReadAllText(filePath);
            rtbScript.UndoRedo.EmptyUndoBuffer();
            rtbScript.Modified = false;
            this.Text = filePath;   //Path.GetFileName(filePath);

            string ext = Path.GetExtension(filePath).ToLower(CultureInfo.CurrentCulture);

            switch (ext)
            {
                case ".cs":
                    tscboLanguage.SelectedIndex = 1;
                    break;

                case ".html":
                    tscboLanguage.SelectedIndex = 7;
                    break;

                case ".htm":
                    tscboLanguage.SelectedIndex = 7;
                    break;

                case ".sql":
                    tscboLanguage.SelectedIndex = 2;
                    break;

                case ".vb":
                    tscboLanguage.SelectedIndex = 3;
                    break;

                case ".xml":
                    tscboLanguage.SelectedIndex = 6;
                    break;

                case ".js":
                    tscboLanguage.SelectedIndex = 4;
                    break;

                case ".lsl":
                    tscboLanguage.SelectedIndex = 0;
                    break;

                case ".php":
                    tscboLanguage.SelectedIndex = 5;
                    break;

                case ".ini":
                    tscboLanguage.SelectedIndex = 8;
                    break;

                case ".aiml":
                    tscboLanguage.SelectedIndex = 9;
                    break;

                case ".txt":
                    tscboLanguage.SelectedIndex = 10;
                    break;

                default:
                    tscboLanguage.SelectedIndex = 10;
                    break;
            }

            tsSaveDisk.Enabled = true;
        }

        private void rtbScript_SelectionChanged(object sender, EventArgs e)
        {
            GetCurrentLine();
            GetCurrentCol();
        }

        private void rtbScript_TextChanged(object sender, EventArgs e)
        {
            if (!rtbScript.IsReadOnly)
            {
                if (!ointernal)
                {
                    tsSave.Enabled = true;
                    tsbSave.Enabled = true;
                }

                tsSaveDisk.Enabled = true;
                //changed = true;
            }
        }

        private void PB1_Click(object sender, EventArgs e)
        {

        }

        private void toolStripDropDownButton1_Click(object sender, EventArgs e)
        {

        }

        private void tsbSave_Click(object sender, EventArgs e)
        {
            SaveScript();
        }
    }
}