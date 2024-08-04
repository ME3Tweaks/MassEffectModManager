﻿//using LegendaryExplorerCore.GameFilesystem;
//using LegendaryExplorerCore.Kismet;
//using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
//using LegendaryExplorerCore.Unreal;
//using LegendaryExplorerCore.Unreal.BinaryConverters;
//using LegendaryExplorerCore.UnrealScript;
//using ME3TweaksCore.GameFilesystem;
//using ME3TweaksCore.Localization;
//using ME3TweaksCore.ME3Tweaks.M3Merge;
//using ME3TweaksCore.Misc;
//using ME3TweaksCoreWPF.Targets;
//using ME3TweaksModManager.modmanager.localizations;
//using Newtonsoft.Json;
//using BioStateEventMap = LegendaryExplorerCore.Unreal.BinaryConverters.BioStateEventMap;

//namespace ME3TweaksModManager.modmanager.merge.game2email
//{
//    public class ME2EmailMerge
//    {

//        private const string EMAIL_MERGE_MANIFEST_FILE = @"EmailMergeInfo.emm";
//        private const string EMAIL_MERGE_FILE_SUFFIX = @".emm";

//        public class ME2EmailMergeFile
//        {
//            /// <summary>
//            /// Game for emails, either ME2 or LE2
//            /// </summary>
//            [JsonProperty(@"game")]
//            public MEGame Game { get; set; }

//            /// <summary>
//            /// Name of mod, used for sequence comments only
//            /// </summary>
//            [JsonProperty(@"modName")]
//            public string ModName { get; set; }

//            /// <summary>
//            /// Optional: id of pre-existing in memory bool indicating mod is installed. Adds extra sanity check when email is sent.
//            /// Email will only send when this bool is true, if field is included
//            /// </summary>
//            [JsonProperty(@"inMemoryBool")]
//            public int? InMemoryBool { get; set; }

//            [JsonProperty(@"emails")]
//            public List<ME2EmailSingle> Emails { get; set; }
//        }

//        public class ME2EmailSingle
//        {
//            /// <summary>
//            /// Internal name for email, used for sequence comments only
//            /// </summary>
//            [JsonProperty(@"emailName")]
//            public string EmailName { get; set; }

//            /// <summary>
//            /// The plot int for your email. User must determine this themselves, as it is written to save
//            /// </summary>
//            [JsonProperty(@"statusPlotInt")]
//            public int StatusPlotInt { get; set; }

//            /// <summary>
//            /// The text of the conditional that will trigger the email. Must include StatusPlotInt == 0, rest is up to user
//            /// </summary>
//            [JsonProperty(@"triggerConditional")]
//            public string TriggerConditional { get; set; }

//            /// <summary>
//            /// String ref of email title
//            /// </summary>
//            [JsonProperty(@"titleStrRef")]
//            public int TitleStrRef { get; set; }

//            /// <summary>
//            /// String ref of email description
//            /// </summary>
//            [JsonProperty(@"descStrRef")]
//            public int DescStrRef { get; set; }

//            /// <summary>
//            /// Optional, transition id to be triggered once email is read.
//            /// We might be able to get rid of this, not sure how useful it will be
//            /// </summary>
//            [JsonProperty(@"readTransition")]
//            public int? ReadTransition { get; set; }
//        }

//        public static string StartupFileName => $@"Startup_{M3MergeDLC.MERGE_DLC_FOLDERNAME}.pcc";

//        /// <summary>
//        /// Returns if the specified target has any email merge files.
//        /// </summary>
//        /// <param name="target"></param>
//        public static bool NeedsMergedGame2(GameTargetWPF target)
//        {
//            if (!target.Game.IsGame2()) return false;
//            try
//            {
//                var emailSupercedances = target.GetFileSupercedances(new[] { @".emm" });
//                return emailSupercedances.TryGetValue(EMAIL_MERGE_MANIFEST_FILE, out var infoList) &&
//                       infoList.Count > 0;
//            }
//            catch (Exception e)
//            {
//                M3Log.Exception(e, @"Error getting file supercedences:");
//            }

//            return false;
//        }

//        /// <summary>
//        /// Runs the email merge feature for game 2. The MergeDLC must already be created for this to work.
//        /// </summary>
//        /// <param name="mergeDLC"></param>
//        /// <exception cref="Exception"></exception>
//        public static string RunGame2EmailMerge(M3MergeDLC mergeDLC, Action<string> progressChanged)
//        {
//            if (!mergeDLC.Generated)
//                return null; // Do not run on non-generated. It may be that a prior check determined this merge was not necessary 

//            var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(mergeDLC.Target.Game, gameRootOverride: mergeDLC.Target.TargetPath, forceReload: true);

//            //DotTrace.EnsurePrerequisite();
//            //var config = new DotTrace.Config();
//            //config.SaveToDir("E:\\sto");
//            //DotTrace.Attach(config);
//            //DotTrace.StartCollectingData();

//            // File to base modifications on
//            loadedFiles.TryGetValue(@"BioD_Nor_103Messages.pcc", out var pccFile);
//            if (pccFile is null) return M3L.GetString(M3L.string_emailsNotMergedMessagesFileNotFound);
//            using IMEPackage pcc = MEPackageHandler.OpenMEPackage(pccFile);

//            // Path to Message templates file - different files for ME2/LE2
//            var resourcesFilePath = $@"ME3TweaksModManager.modmanager.merge.game2email.{mergeDLC.Target.Game}.103Message_Templates_{mergeDLC.Target.Game}.pcc";
//            using IMEPackage resources = MEPackageHandler.OpenMEPackageFromStream(M3Utilities.GetResourceStream(resourcesFilePath));

//            // Startup file to place conditionals and transitions into
//            var startupInstalled = loadedFiles.ContainsKey(StartupFileName);
//            using IMEPackage startup = startupInstalled
//                ? MEPackageHandler.OpenMEPackage(loadedFiles[StartupFileName])
//                : MEPackageHandler.OpenMEPackageFromStream(M3Utilities.GetResourceStream($@"ME3TweaksModManager.modmanager.merge.dlc.{mergeDLC.Target.Game}.{StartupFileName}"), StartupFileName);

//            var emailInfos = new List<ME2EmailMergeFile>();
//            var jsonSupercedances = M3Directories.GetFileSupercedances(mergeDLC.Target, new[] { EMAIL_MERGE_FILE_SUFFIX });
//            if (jsonSupercedances.TryGetValue(EMAIL_MERGE_MANIFEST_FILE, out var jsonList))
//            {
//                jsonList.Reverse();
//                foreach (var dlc in jsonList)
//                {
//                    var jsonFile = Path.Combine(M3Directories.GetDLCPath(mergeDLC.Target), dlc, mergeDLC.Target.Game.CookedDirName(),
//                        EMAIL_MERGE_MANIFEST_FILE);
//                    emailInfos.Add(JsonConvert.DeserializeObject<ME2EmailMergeFile>(File.ReadAllText(jsonFile)));
//                }
//            }

//            // Sanity checks
//            if (!Enumerable.Any(emailInfos) || !emailInfos.SelectMany(e => e.Emails).Any())
//            {
//                return null; // No emails
//            }

//            if (emailInfos.Any(e => e.Game != mergeDLC.Target.Game))
//            {
//                throw new Exception(M3L.GetString(M3L.string_game2EmailMergeWrongGame));
//            }

//            // Startup File
//            ExportEntry stateEventMapExport = startup.Exports
//                .FirstOrDefault(e => e.ClassName == @"BioStateEventMap" && e.ObjectName == @"StateTransitionMap");
//            BioStateEventMap StateEventMap = stateEventMapExport.GetBinaryData<BioStateEventMap>();

//            // Setup conditionals
//            ExportEntry ConditionalClass = startup.FindExport($@"PlotManager{M3MergeDLC.MERGE_DLC_FOLDERNAME}.BioAutoConditionals");
//            FileLib fl = new FileLib(startup);
//            bool initialized = fl.Initialize(new TargetPackageCache() { RootPath = M3Directories.GetBioGamePath(mergeDLC.Target) }, gameRootPath: mergeDLC.Target.TargetPath);
//            if (!initialized)
//            {
//                throw new Exception(
//                    $@"FileLib for script update could not initialize, cannot install conditionals");
//            }

//            #region Sequence Exports
//            // Send message - All email conditionals are checked and emails transitions are triggered
//            ExportEntry SendMessageContainer = pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Send_Messages");
//            ExportEntry LastSendMessage = KismetHelper.GetSequenceObjects(SendMessageContainer).OfType<ExportEntry>()
//                .FirstOrDefault(e =>
//                {
//                    var outbound = SeqTools.GetOutboundLinksOfNode(e);
//                    return outbound.Count == 1 && outbound[0].Count == 0;
//                });
//            ExportEntry TemplateSendMessage = resources.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Send_MessageTemplate");
//            ExportEntry TemplateSendMessageBoolCheck = resources.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Send_MessageTemplate_BoolCheck");


//            // Mark Read - email ints are set to read
//            // This is the only section that does not gracefully handle different DLC installations - DLC_CER is required atm
//            ExportEntry MarkReadContainer = pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Mark_Read");
//            ExportEntry LastMarkRead = pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Mark_Read.DLC_CER");
//            ExportEntry MarkReadOutLink = SeqTools.GetOutboundLinksOfNode(LastMarkRead)[0][0].LinkedOp as ExportEntry;
//            KismetHelper.RemoveOutputLinks(LastMarkRead);

//            ExportEntry TemplateMarkRead = resources.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Mark_ReadTemplate");
//            ExportEntry TemplateMarkReadTransition = resources.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Mark_Read_Transition");


//            // Display Messages - Str refs are passed through to GUI
//            ExportEntry DisplayMessageContainer =
//                pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Display_Messages");
//            ExportEntry DisplayMessageOutLink =
//                pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Display_Messages.SeqCond_CompareBool_0"); // This is the last thing before finish sequence

//            ExportEntry LastDisplayMessage = SeqTools.FindOutboundConnectionsToNode(DisplayMessageOutLink, KismetHelper.GetSequenceObjects(DisplayMessageContainer).OfType<ExportEntry>())[0];
//            KismetHelper.RemoveOutputLinks(LastDisplayMessage);
//            var DisplayMessageVariableLinks = LastDisplayMessage.GetProperty<ArrayProperty<StructProperty>>(@"VariableLinks");
//            ExportEntry TemplateDisplayMessage =
//                resources.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Display_MessageTemplate");

//            // Archive Messages - Message ints are set to 3
//            ExportEntry ArchiveContainer = pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Archive_Message");
//            ExportEntry ArchiveSwitch = pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Archive_Message.SeqAct_Switch_0");
//            ExportEntry ArchiveOutLink =
//                pcc.FindExport(
//                    @"TheWorld.PersistentLevel.Main_Sequence.Archive_Message.BioSeqAct_PMCheckConditional_1");
//            ExportEntry ExampleSetInt = SeqTools.GetOutboundLinksOfNode(ArchiveSwitch)[0][0].LinkedOp as ExportEntry;
//            ExportEntry ExamplePlotInt = SeqTools.GetVariableLinksOfNode(ExampleSetInt)[0].LinkedNodes[0] as ExportEntry;
//            #endregion

//            var cache = new TargetPackageCache() { RootPath = mergeDLC.Target.TargetPath }; // This significantly improves performance
//            foreach (var v in EntryImporter.FilesSafeToImportFrom(mergeDLC.Target.Game))
//            {
//                var f = cache.GetCachedPackage(loadedFiles[v]);
//            }

//            int messageID = SeqTools.GetOutboundLinksOfNode(ArchiveSwitch).Count + 1;
//            int currentSwCount = ArchiveSwitch.GetProperty<IntProperty>(@"LinkCount").Value;
//            int done = 0;
//            int totalEmails = emailInfos.Sum(x => x.Emails.Count);
//            foreach (var emailMod in emailInfos)
//            {
//                string modName = @"DLC_MOD_" + emailMod.ModName;

//                foreach (var email in emailMod.Emails)
//                {
//                    M3Log.Information($@"Merging email {email.EmailName}");
//                    string emailName = modName + @"_" + email.EmailName;
//                    if (string.IsNullOrEmpty(email.TriggerConditional))
//                    {
//                        email.TriggerConditional =
//                            $@"local BioGlobalVariableTable gv;gv = bioWorld.GetGlobalVariables();return gv.GetInt({email.StatusPlotInt}) == 0;";
//                    }

//                    // Create send transition
//                    int transitionId = WriteTransition(mergeDLC, StateEventMap, email.StatusPlotInt);
//                    int conditionalId = WriteConditional(mergeDLC, ConditionalClass, fl, email.TriggerConditional, cache);

//                    #region SendMessage
//                    //////////////
//                    // SendMessage
//                    //////////////

//                    // Create seq object
//                    var SMTemp = emailMod.InMemoryBool.HasValue ? TemplateSendMessageBoolCheck : TemplateSendMessage;
//                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild,
//                        SMTemp,
//                        pcc, SendMessageContainer, true, new RelinkerOptionsPackage(), out var outSendEntry);

//                    var newSend = outSendEntry as ExportEntry;

//                    // Set name, comment, add to sequence
//                    newSend.ObjectName = new NameReference(emailName);
//                    KismetHelper.AddObjectToSequence(newSend, SendMessageContainer);
//                    KismetHelper.SetComment(newSend, emailName);
//                    if (mergeDLC.Target.Game == MEGame.ME2) newSend.WriteProperty(new StrProperty(emailName, @"ObjName"));

//                    // Set Trigger Conditional
//                    var pmCheckConditionalSM = newSend.GetChildren()
//                        .FirstOrDefault(e => e.ClassName == @"BioSeqAct_PMCheckConditional" && e is ExportEntry);
//                    if (pmCheckConditionalSM is ExportEntry conditional)
//                    {
//                        conditional.WriteProperty(new IntProperty(conditionalId, @"m_nIndex"));
//                        KismetHelper.SetComment(conditional, @$"Time for {email.EmailName}?");
//                    }

//                    // Set Send Transition
//                    var pmExecuteTransitionSM = newSend.GetChildren()
//                        .FirstOrDefault(e => e.ClassName == @"BioSeqAct_PMExecuteTransition" && e is ExportEntry);
//                    if (pmExecuteTransitionSM is ExportEntry transition)
//                    {
//                        transition.WriteProperty(new IntProperty(transitionId, @"m_nIndex"));
//                        KismetHelper.SetComment(transition, @$"Send {email.EmailName} message.");

//                    }

//                    // Set Send Transition
//                    if (emailMod.InMemoryBool.HasValue)
//                    {
//                        var pmCheckStateSM = newSend.GetChildren()
//                            .FirstOrDefault(e => e.ClassName == @"BioSeqAct_PMCheckState" && e is ExportEntry);
//                        if (pmCheckStateSM is ExportEntry checkState)
//                        {
//                            checkState.WriteProperty(new IntProperty(emailMod.InMemoryBool.Value, @"m_nIndex"));
//                            KismetHelper.SetComment(checkState, @$"Is {emailMod.ModName} installed?");
//                        }
//                    }

//                    // Hook up output links
//                    KismetHelper.CreateOutputLink(LastSendMessage, @"Out", newSend);
//                    LastSendMessage = newSend;
//                    #endregion

//                    #region MarkRead
//                    ///////////
//                    // MarkRead
//                    ///////////

//                    // Create seq object
//                    var mrTemplate = email.ReadTransition.HasValue ? TemplateMarkReadTransition : TemplateMarkRead;
//                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild,
//                        mrTemplate, pcc, MarkReadContainer, true, new RelinkerOptionsPackage(), out var outMarkReadEntry);
//                    var newMarkRead = outMarkReadEntry as ExportEntry;

//                    // Set name, comment, add to sequence
//                    newMarkRead.ObjectName = new NameReference(emailName);
//                    KismetHelper.AddObjectToSequence(newMarkRead, MarkReadContainer);
//                    KismetHelper.SetComment(newMarkRead, emailName);
//                    if (mergeDLC.Target.Game == MEGame.ME2) newMarkRead.WriteProperty(new StrProperty(emailName, @"ObjName"));

//                    // Set Plot Int
//                    var storyManagerIntMR = newMarkRead.GetChildren()
//                        .FirstOrDefault(e => e.ClassName == @"BioSeqVar_StoryManagerInt" && e is ExportEntry);
//                    if (storyManagerIntMR is ExportEntry plotIntMR)
//                    {
//                        plotIntMR.WriteProperty(new IntProperty(email.StatusPlotInt, @"m_nIndex"));
//                        KismetHelper.SetComment(plotIntMR, email.EmailName);
//                    }

//                    if (email.ReadTransition.HasValue)
//                    {
//                        var pmExecuteTransitionMR = newMarkRead.GetChildren()
//                            .FirstOrDefault(e => e.ClassName == @"BioSeqAct_PMExecuteTransition" && e is ExportEntry);
//                        if (pmExecuteTransitionMR is ExportEntry transitionMR)
//                        {
//                            transitionMR.WriteProperty(new IntProperty(email.ReadTransition.Value, @"m_nIndex"));
//                            KismetHelper.SetComment(transitionMR, @$"Trigger {email.EmailName} read transition");
//                        }
//                    }

//                    // Hook up output links
//                    KismetHelper.CreateOutputLink(LastMarkRead, @"Out", newMarkRead);
//                    LastMarkRead = newMarkRead;
//                    #endregion

//                    #region DisplayEmail
//                    ////////////////
//                    // Display Email
//                    ////////////////

//                    // Create seq object
//                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild,
//                        TemplateDisplayMessage, pcc, DisplayMessageContainer, true, new RelinkerOptionsPackage(), out var outDisplayMessage);
//                    var newDisplayMessage = outDisplayMessage as ExportEntry;

//                    // Set name, comment, variable links, add to sequence
//                    newDisplayMessage.ObjectName = new NameReference(emailName);
//                    KismetHelper.AddObjectToSequence(newDisplayMessage, DisplayMessageContainer);
//                    newDisplayMessage.WriteProperty(DisplayMessageVariableLinks);
//                    KismetHelper.SetComment(newDisplayMessage, emailName);
//                    if (mergeDLC.Target.Game == MEGame.ME2) newDisplayMessage.WriteProperty(new StrProperty(emailName, @"ObjName"));

//                    var displayChildren = newDisplayMessage.GetChildren().ToList();

//                    // Set Plot Int
//                    var storyManagerIntDE = displayChildren.FirstOrDefault(e =>
//                        e.ClassName == @"BioSeqVar_StoryManagerInt" && e is ExportEntry);
//                    if (storyManagerIntDE is ExportEntry plotIntDe)
//                    {
//                        plotIntDe.WriteProperty(new IntProperty(email.StatusPlotInt, @"m_nIndex"));
//                    }

//                    // Set Email ID
//                    var emailIdDE = displayChildren.FirstOrDefault(e =>
//                        e.ClassName == @"SeqVar_Int" && e is ExportEntry);
//                    if (emailIdDE is ExportEntry emailIdDe)
//                    {
//                        emailIdDe.WriteProperty(new IntProperty(messageID, @"IntValue"));
//                    }

//                    // Set Title StrRef
//                    var titleStrRef = displayChildren.FirstOrDefault(e =>
//                        e.ClassName == @"BioSeqVar_StrRef" && e is ExportEntry ee && ee.GetProperty<NameProperty>(@"VarName").Value == @"Title StrRef");
//                    if (titleStrRef is ExportEntry title)
//                    {
//                        title.WriteProperty(new StringRefProperty(email.TitleStrRef, @"m_srValue"));
//                    }

//                    // Set Description StrRef
//                    var descStrRef = displayChildren.FirstOrDefault(e =>
//                        e.ClassName == @"BioSeqVar_StrRef" && e is ExportEntry ee && ee.GetProperty<NameProperty>(@"VarName").Value == @"Desc StrRef");
//                    if (descStrRef is ExportEntry desc)
//                    {
//                        desc.WriteProperty(new StringRefProperty(email.DescStrRef, @"m_srValue"));
//                    }

//                    // Hook up output links
//                    KismetHelper.CreateOutputLink(LastDisplayMessage, @"Out", newDisplayMessage);
//                    LastDisplayMessage = newDisplayMessage;
//                    #endregion

//                    #region ArchiveEmail
//                    ////////////////
//                    // Archive Email
//                    ////////////////

//                    var newSetInt = EntryCloner.CloneEntry(ExampleSetInt);
//                    KismetHelper.AddObjectToSequence(newSetInt, ArchiveContainer);
//                    KismetHelper.CreateOutputLink(newSetInt, @"Out", ArchiveOutLink);

//                    KismetHelper.CreateNewOutputLink(ArchiveSwitch, @"Link " + (messageID - 1), newSetInt);

//                    var newPlotInt = EntryCloner.CloneEntry(ExamplePlotInt);
//                    KismetHelper.AddObjectToSequence(newPlotInt, ArchiveContainer);
//                    newPlotInt.WriteProperty(new IntProperty(email.StatusPlotInt, @"m_nIndex"));
//                    newPlotInt.WriteProperty(new StrProperty(emailName, @"m_sRefName"));

//                    var linkedVars = SeqTools.GetVariableLinksOfNode(newSetInt);
//                    linkedVars[0].LinkedNodes = new List<IEntry>() { newPlotInt };
//                    SeqTools.WriteVariableLinksToNode(newSetInt, linkedVars);

//                    messageID++;
//                    currentSwCount++;
//                    #endregion

//                    progressChanged?.Invoke($@"{M3L.GetString(M3L.string_synchronizingEmails)} {(int)(done++ * 100.0f / totalEmails)}%");
//                }
//            }
//            KismetHelper.CreateOutputLink(LastMarkRead, @"Out", MarkReadOutLink);
//            KismetHelper.CreateOutputLink(LastDisplayMessage, @"Out", DisplayMessageOutLink);
//            ArchiveSwitch.WriteProperty(new IntProperty(currentSwCount, @"LinkCount"));

//            stateEventMapExport.WriteBinary(StateEventMap);

//            // Relink the conditionals chain
//            UClass uc = ObjectBinary.From<UClass>(ConditionalClass);
//            uc.UpdateLocalFunctions();
//            uc.UpdateChildrenChain();
//            ConditionalClass.WriteBinary(uc);

//            // Save Messages file into DLC
//            var mergeDlcCookedDir = Path.Combine(M3Directories.GetDLCPath(mergeDLC.Target), M3MergeDLC.MERGE_DLC_FOLDERNAME, mergeDLC.Target.Game.CookedDirName());
//            var outMessages = Path.Combine(mergeDlcCookedDir, @"BioD_Nor_103Messages.pcc");
//            pcc.Save(outMessages);

//            // Save Startup file into DLC
//            var startupF = Path.Combine(mergeDlcCookedDir, StartupFileName);
//            startup.Save(startupF);

//            // Make sure lines are written to BIOEngine.ini and BIOGame.ini
//            M3MergeDLC.AddPlotDataToConfig(mergeDLC);

//            //DotTrace.SaveData(); // End profiling
//            //DotTrace.Detach();
//            return null; // OK
//        }

//        /// <summary>
//        /// Writes the conditional to the startup package
//        /// </summary>
//        /// <param name="conditionalClass"></param>
//        /// <param name="fl"></param>
//        /// <param name="innerFunctionText"></param>
//        /// <returns>ID of new conditional</returns>
//        private static int WriteConditional(M3MergeDLC mergeDLC, ExportEntry conditionalClass, FileLib fl, string innerFunctionText, PackageCache cache)
//        {
//            // Add Conditional Functions
//            var conditionalId = mergeDLC.CurrentConditional++;

//            //var funcToClone = conditionalClass.FileRef.FindExport($@"{conditionalClass.InstancedFullPath}.TemplateFunction");
//            //var func = EntryCloner.CloneTree(funcToClone);
//            //func.ObjectName = $@"F{conditionalId}";
//            //func.indexValue = 0;

//            var fullFunctionText = $@"public function bool F{conditionalId}(BioWorldInfo bioWorld, int Argument) {{ {innerFunctionText} }}";
//            var log = UnrealScriptCompiler.AddOrReplaceInClass(conditionalClass, fullFunctionText, fl, cache);
//            if (log.AllErrors.Any())
//            {
//                M3Log.Error($@"Error compiling function F{conditionalId}");
//                foreach (var l in log.AllErrors)
//                {
//                    M3Log.Error(l.Message);
//                }

//                throw new Exception(LC.GetString(LC.string_interp_errorCompilingConditionalFunction, $@"F{conditionalId}", string.Join('\n', log.AllErrors.Select(x => x.Message))));
//            }

//            return conditionalId;
//        }

//        /// <summary>
//        /// Creates a Send Email transition for the given int in the state event map, returning the transition id
//        /// </summary>
//        /// <param name="map"></param>
//        /// <param name="integerId"></param>
//        /// <returns></returns>
//        private static int WriteTransition(M3MergeDLC mergeDLC, BioStateEventMap map, int integerId)
//        {
//            var transition = new BioStateEventMap.BioStateEvent();
//            transition.Elements = new List<BioStateEventMap.BioStateEventElement>();

//            // Set Unread_Messages_Exist to true
//            transition.Elements.Add(new BioStateEventMap.BioStateEventElementBool
//            {
//                GlobalBool = 4328,
//                NewState = true,
//                Type = BioStateEventMap.BioStateEventElementType.Bool
//            });

//            // Set Yeoman_Needs_To_Comment to true
//            transition.Elements.Add(new BioStateEventMap.BioStateEventElementBool
//            {
//                GlobalBool = 4321,
//                NewState = true,
//                Type = BioStateEventMap.BioStateEventElementType.Bool
//            });

//            // Set email int to 1 (sent and unread)
//            transition.Elements.Add(new BioStateEventMap.BioStateEventElementInt
//            {
//                GlobalInt = integerId,
//                NewValue = 1,
//                InstanceVersion = 1,
//                Type = BioStateEventMap.BioStateEventElementType.Int
//            });

//            transition.ID = mergeDLC.CurrentTransition++;
//            map.StateEvents.Add(transition);

//            return transition.ID;
//        }
//    }
//}
