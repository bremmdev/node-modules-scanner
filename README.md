# node-modules-scanner

A small C# command-line utility for finding `node_modules` directories, estimating their size, and sorting them by last modified date so stale dependencies are easy to spot.

It scans a starting directory or, if none is provided, all ready drives on Windows. Results are shown in a color-coded table with a running status display during the scan.

## Features

- Finds `node_modules` directories without recursing into them after they are found.
- Calculates directory size while skipping symlinks and junctions to avoid double-counting.
- Sorts results by last modified date.
- Filters out empty results.
- Supports excluding directory names from the scan.
- Supports filtering by minimum age in days.
- Shows live scan status with elapsed time, scanned directory count, and matches found.

## Requirements

- .NET with support for file-based apps and `#:package` directives.
- Windows is the primary target, especially when scanning all drives by default.

## Usage

Run the app with an optional starting path:

```bash
dotnet run Program.cs -- "C:\path\to\search"
```

Run against all ready drives:

```bash
dotnet run Program.cs
```

## Options

```text
root-path                     Starting directory for the search
-m, --min-modified-ago       Only include directories modified longer than X days ago
-e, --exclude                Comma-separated list of directory names to exclude
```

## Examples

Scan a single project tree:

```bash
dotnet run Program.cs -- "C:\dev"
```

Only show `node_modules` folders older than 90 days:

```bash
dotnet run Program.cs -- "C:\dev" --min-modified-ago 90
```

Exclude common folders from scanning:

```bash
dotnet run Program.cs -- "C:\dev" --exclude .git,dist,build
```

Combine both filters:

```bash
dotnet run Program.cs -- "C:\dev" --min-modified-ago 180 --exclude .git,dist,build
```

## Output

The final output includes:

- A table with the folder path, estimated size in MB, and last modified date.
- Color-coded rows:
  - Green: modified within the last 90 days
  - Yellow: modified 91 to 180 days ago
  - Red: modified more than 180 days ago
- A summary showing total `node_modules` size and total directories scanned.

During scanning, the status line shows:

- Elapsed time
- Number of directories scanned
- Number of `node_modules` directories found

## Notes

- Protected or inaccessible directories are skipped.
- If a `node_modules` directory is found, it is recorded but not searched further.
- Size is reported as the sum of file sizes, not on-disk allocation size.
- Empty `node_modules` directories are omitted from the final table.
