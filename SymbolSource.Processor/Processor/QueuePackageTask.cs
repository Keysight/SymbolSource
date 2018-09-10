﻿using System;
using System.Threading.Tasks;
using NuGet;
using SymbolSource.Contract;
using SymbolSource.Processor.Notifier;
using PackageName = SymbolSource.Contract.PackageName;
using System.IO;

namespace SymbolSource.Processor.Processor
{
    public class QueuePackageTask : PackageTask
    {
        private static string GetRandomAlphaNumeric()
        {
            return Path.GetRandomFileName().Replace(".", "").Substring(0, 4);
        }

        public async Task<PackageName> ReadName(UserInfo userInfo, ZipPackage package)
        {
            var suffix = "-at-" + DateTime.Now.ToEncodedSeconds() + GetRandomAlphaNumeric();
            var newVersion = package.Version.ToString().AddSuffix(suffix, MaxSemanticVersionLength, '.');
            var newPackageName = new PackageName(package.Id, newVersion);
            return newPackageName;
        }
    }
}