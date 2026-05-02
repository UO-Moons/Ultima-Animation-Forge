using System.Collections.Generic;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public interface IAnimationDataSource
{
    bool Initialize(string folderPath);

    string FolderPath { get; }
    string SourceMode { get; }

    Dictionary<int, BodyConvEntry> BodyConvEntries { get; }
    Dictionary<int, MobTypeEntry> MobTypeEntries { get; }

    string GetBodyTypeName(int bodyId);
    int GetGroupCountForBody(int bodyId);

    List<int> GetAvailableActionIndices(int bodyId);

    List<AnimationEntry> BuildAnimationEntries(int maxBodyId);

    bool TryResolveAnimationBlock(int bodyId, int actionIndex, int directionIndex, out ResolvedAnimationBlock resolvedBlock);

    byte[] ReadAnimationBlock(ResolvedAnimationBlock resolvedBlock);
}