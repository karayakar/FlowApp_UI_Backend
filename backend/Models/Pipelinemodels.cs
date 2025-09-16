// Models.cs
// Bu dosya, eldeki model tanımlarını BOZMAMAK için "partial" kullanır.
// Elindeki mevcut FlowSnapshot / FlowNode / FlowEdge tanımlarına ek alanlar ve DTO'lar eklenir.

using System;
using System.Collections.Generic;

#nullable enable

// -----------------------------
// FlowSnapshot genişletmesi
// -----------------------------
public partial class FlowSnapshot
{
    /// <summary>
    /// Her pipeline için kalıcı/tekil GUID. Dosyaya kayıtta dosya adı olarak da kullanılır.
    /// Eğer UI'dan gelmiyorsa ilk normalize sırasında atanır.
    /// </summary>
    public string PipelineId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// UX için isteğe bağlı isim/başlık. Yoksa "Flow {PipelineId}" olarak yorumlanır.
    /// </summary>
    public string? Title { get; set; }
    public int Version { get; set; } = 1;
    public List<FlowNode> Nodes { get; set; } = new();
    public List<FlowEdge> Edges { get; set; } = new();
}

// -----------------------------
// Yardımcı DTO'lar
// -----------------------------

/// <summary>
/// Flows klasöründe kaydedilmiş snapshot'ların indeks öğesi.
/// </summary>
public sealed class StoredFlowIndexItem
{
    public string PipelineId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public DateTime SavedAtUtc { get; set; }
    public string FileName { get; set; } = string.Empty;
}

/// <summary>
/// /api/flows dönüş modeli
/// </summary>
public sealed class StoredFlowListResponse
{
    public bool Ok { get; set; } = true;
    public List<StoredFlowIndexItem> Items { get; set; } = new();
}

/// <summary>
/// /api/flows/{id} dönüş modeli
/// </summary>
public sealed class StoredFlowGetResponse
{
    public bool Ok { get; set; } = true;
    public FlowSnapshot? Snapshot { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// /api/execute? id=... çağrısında döndürmek için basit sonuç modeli (Debug outputs / OutputStore üzerinden zaten erişilebilir)
/// </summary>
public sealed class ExecuteResultDto
{
    public bool Ok { get; set; }
    public object? Outputs { get; set; }
    public string? Error { get; set; }
}
