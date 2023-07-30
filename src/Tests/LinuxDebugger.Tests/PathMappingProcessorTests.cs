using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinuxDebugger.ProjectSystem;
using LinuxDebugger.ProjectSystem.Deployment;
using LinuxDebugger.ProjectSystem.Deployment.Providers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sodiware.VisualStudio.Linux.Tests
{
    [TestClass]
    public class PathMappingProcessorTests
    {
        PathMappingProcessor sut;
        [TestInitialize]
        public void TestInitialize()
        {
        }

        [TestMethod]
        public void PathMappingProcessor__can_sort_files()
        {
            const string slnDir = @"C:\Solution";
            const string projectName = "Project1";
            var dir1 = new DirectoryHandle(slnDir);
            var dir2 = new DirectoryHandle(Path.Combine(slnDir, projectName));

            sut = new(dir2, dir1);

            sut.Add(Path.Combine(slnDir, "file1"));
            Assert.AreEqual(PathMappingRoot.Solution, sut.Root);
            Assert.IsNotNull(sut.SolutionDir);
            Assert.AreEqual(1, sut.SolutionDir.Count);
        }

        [TestMethod]
        public void PathMappingProcessor__can_map_files_outside_directory()
        {
            const string slnDir = @"C:\Solution";
            const string projectName = "Project1";
            var dir1 = new DirectoryHandle(slnDir);
            var dir2 = new DirectoryHandle(Path.Combine(slnDir, projectName));

            sut = new(dir2, dir1);

            sut.Add(@"C:\foo\bar\file1");
            sut.Add(@"C:\foo\file");
            Assert.IsTrue(sut.HasOutsideFiles);
        }
    }
}
