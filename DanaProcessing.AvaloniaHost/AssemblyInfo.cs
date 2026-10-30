using System.Runtime.CompilerServices;

// Tests exercise the attached-sketch plumbing that doesn't need a desktop
// (key mapping, hit geometry) without making it public API.
[assembly: InternalsVisibleTo("DanaProcessing.Tests")]
