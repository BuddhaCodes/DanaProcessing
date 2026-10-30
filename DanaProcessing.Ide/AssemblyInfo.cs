using System.Runtime.CompilerServices;

// The tests reach the export runner's command-line jobs and single-instance guard.
[assembly: InternalsVisibleTo("DanaProcessing.Tests")]
