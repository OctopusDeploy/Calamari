# YamlJson6902PatchImageReplacer Result Consistency Design

**Date:** 2026-03-30
**Status:** Approved
**Context:** Addressing PR #1835 review comments about return value consistency

## Problem Statement

The `YamlJson6902PatchImageReplacer` class has inconsistent return patterns across its methods. Some methods return `List<string>` for replacements while others use different patterns, making the code harder to reason about and test.

Review comments specifically requested:
1. Return the replacementsMade rather than boolean - an empty list means nothing was done
2. Return an empty list if nothing was done

## Solution: Consistent Result Objects

Standardize all processing methods in `YamlJson6902PatchImageReplacer` to return `ImageReplacementResult` objects, providing consistent encapsulation of both content changes and replacement tracking.

## Design Details

### Current State
Methods currently return `List<string>` for replacement tracking:
- `ProcessPatchOperation`
- `ProcessReplaceOperation`
- `ProcessAddOperation`
- `ProcessContainersSequence`
- `ProcessContainerMapping`
- `ProcessImageReference`

### Changes Required

**1. Method Return Type Standardization**
- All processing methods return `ImageReplacementResult`
- Empty `UpdatedImageReferences` set indicates no replacements made
- `UpdatedContents` contains modified content (or original if unchanged)

**2. Result Composition Pattern**
- Methods processing multiple items combine results using helper method
- Content updates flow through the processing chain
- Replacement sets are merged at each level

**3. Helper Method Addition**
```csharp
private static ImageReplacementResult CombineResults(
    string currentContent,
    params ImageReplacementResult[] results)
```

**4. NoChangeResult Pattern Extension**
- Expand existing `NoChangeResult` usage to all methods
- Consistent "no changes" behavior across all processing levels
- Methods return either `NoChangeResult` or new result with changes

### Benefits

**Code Quality**
- Eliminates mixed return types
- Consistent error handling and null checking
- Better encapsulation of processing results

**Testing**
- Every method has same result structure
- Easier to verify both content changes and replacement tracking
- Consistent assertions across all test cases

**Maintainability**
- Uniform pattern for future method additions
- Clear contract for all processing methods
- Consistent with existing public API

## Implementation Notes

- Preserve existing `NoChangeResult` property behavior
- Maintain backward compatibility at public API level
- Update internal method calls to handle `ImageReplacementResult` returns
- Add result combination logic where multiple operations are processed

## Success Criteria

- All internal processing methods return `ImageReplacementResult`
- No breaking changes to public API
- All existing tests continue to pass
- Code is more consistent and easier to reason about