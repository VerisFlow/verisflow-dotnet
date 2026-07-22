using System;
using System.IO;
using Microsoft.Extensions.Options;
using Moq;
using TraceLogic.Core.Enums;
using TraceLogic.Core.IO;
using TraceLogic.Core.Options;
using Xunit;

namespace TraceLogic.Core.Tests.IO
{
    /// <summary>
    /// Contains unit tests for validating file discovery and time-based filtering in <see cref="TraceLocator"/>.
    /// </summary>
    public class TraceLocatorTests
    {
        /// <summary>
        /// Verifies that <see cref="TraceLocator.FindFiles"/> throws a <see cref="DirectoryNotFoundException"/> when the configured directory path does not exist.
        /// </summary>
        [Fact]
        public void FindFiles_DirectoryDoesNotExist_ShouldThrowDirectoryNotFoundException()
        {
            // Arrange
            var optionsMock = new Mock<IOptions<TraceLocatorOptions>>();
            optionsMock.Setup(o => o.Value).Returns(new TraceLocatorOptions
            {
                DefaultLogDirectory = @"C:\NonExistentDirectory_TraceLogic_Test"
            });

            var locator = new TraceLocator(optionsMock.Object);

            // Act & Assert
            Assert.Throws<DirectoryNotFoundException>(() => locator.FindFiles(TimeFilterType.All));
        }

        /// <summary>
        /// Verifies that applying <see cref="TimeFilterType.Latest"/> filters out older files and returns only the most recently modified trace log file.
        /// </summary>
        [Fact]
        public void FindFiles_LatestFilter_ShouldReturnOnlyMostRecentFile()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                string file1 = Path.Combine(tempDir, "old.trc");
                string file2 = Path.Combine(tempDir, "new.trc");

                File.WriteAllText(file1, "dummy log");
                File.WriteAllText(file2, "dummy log");

                // Explicitly set modification times
                File.SetLastWriteTime(file1, DateTime.Now.AddHours(-2));
                File.SetLastWriteTime(file2, DateTime.Now);

                var optionsMock = new Mock<IOptions<TraceLocatorOptions>>();
                optionsMock.Setup(o => o.Value).Returns(new TraceLocatorOptions { DefaultLogDirectory = tempDir });

                var locator = new TraceLocator(optionsMock.Object);

                // Act
                var result = locator.FindFiles(TimeFilterType.Latest);

                // Assert
                Assert.Single(result);
                Assert.Equal("new.trc", result[0].FileName);
            }
            finally
            {
                // Clean up temporary directory and files created for testing
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}