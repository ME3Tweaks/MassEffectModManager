﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using ME3TweaksCore.Helpers;
using ME3TweaksCore.Services.Backup;
using ME3TweaksModManager.modmanager;
using ME3TweaksModManager.modmanager.me3tweaks;
using ME3TweaksModManager.modmanager.usercontrols;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ME3TweaksModManager.Tests
{
    [TestClass]
    public class MixinTests
    {
        [TestMethod]
        public void TestMixins()
        {
            GlobalTest.Init();

            var me3BackupPath = BackupService.GetGameBackupPath(MEGame.ME3);
            if (me3BackupPath != null)
            {
                GlobalTest.CreateScratchDir();
                MixinHandler.LoadME3TweaksPackage();
                // We can conduct this test
                var mixins = MixinHandler.ME3TweaksPackageMixins.Where(x => !x.IsFinalizer).ToList();
                MixinHandler.LoadPatchDataForMixins(mixins);

                List<string> failedMixins = new List<string>();
                void failedApplicationCallback(string str)
                {
                    failedMixins.Add(str);
                }
                var compilingListsPerModule = MixinHandler.GetMixinApplicationList(mixins, failedApplicationCallback);

                //Mixins are ready to be applied
                var outdir = Path.Combine(Path.Combine(GlobalTest.GetScratchDir(), "MixinTest"));
                MUtilities.DeleteFilesAndFoldersRecursively(outdir);
                Directory.CreateDirectory(outdir);
                foreach (var mapping in compilingListsPerModule)
                {
                    MixinManager.ApplyMixinsToModule(mapping, outdir, null, failedApplicationCallback);
                }
                
                MixinHandler.FreeME3TweaksPatchData();
                GlobalTest.DeleteScratchDir();
                if (failedMixins.Any())
                {
                    Assert.Fail($"MixinTests failed. {failedMixins.Count} mixins failed to apply.");
                }
            }
            else
            {
                Console.WriteLine(@"No backup for ME3 is available. MixinTests will be skipped.");
            }
        }
    }
}
