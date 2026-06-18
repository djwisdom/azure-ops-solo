using System.IO;
using System.Text.Json;

namespace MyCrownJewelApp.Pfpad;

public record FavItem(
    string Id,
    string Name,
    string? Url,
    string? ParentId,
    int Order,
    bool IsFolder
);

public sealed class FavoritesService
{
    private static readonly Lazy<FavoritesService> _instance = new(() => new FavoritesService());
    private readonly string _storagePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MyCrownJewelApp",
        "favorites.json");

    public static FavoritesService Instance => _instance.Value;

    public List<FavItem> Items { get; } = [];

    public event Action? Changed;

    private FavoritesService()
    {
        Load();
    }

    public void Load()
    {
        Items.Clear();

        if (!File.Exists(_storagePath))
            return;

        try
        {
            var loaded = JsonSerializer.Deserialize<List<FavItem>>(File.ReadAllText(_storagePath));
            if (loaded != null)
                Items.AddRange(loaded);
            NormalizeAllOrders();
        }
        catch
        {
            Items.Clear();
        }
    }

    public void Save()
    {
        string? dir = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(_storagePath, JsonSerializer.Serialize(Items, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    public FavItem AddUrl(string name, string url, string? parentId = null)
    {
        parentId = NormalizeParentId(parentId);
        var item = new FavItem(
            Guid.NewGuid().ToString(),
            SanitizeName(name),
            url.Trim(),
            parentId,
            GetNextOrder(parentId),
            false);

        Items.Add(item);
        PersistAndNotify();
        return item;
    }

    public FavItem AddFolder(string name, string? parentId = null)
    {
        parentId = NormalizeParentId(parentId);
        var item = new FavItem(
            Guid.NewGuid().ToString(),
            SanitizeName(name),
            null,
            parentId,
            GetNextOrder(parentId),
            true);

        Items.Add(item);
        PersistAndNotify();
        return item;
    }

    public void Delete(string id)
    {
        var item = FindById(id);
        if (item == null)
            return;

        var idsToRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { id };
        CollectDescendantIds(id, idsToRemove);
        Items.RemoveAll(item => idsToRemove.Contains(item.Id));
        PersistAndNotify();
    }

    public void Rename(string id, string newName)
    {
        var item = FindById(id);
        if (item == null)
            return;

        SetItem(item with { Name = SanitizeName(newName) });
        PersistAndNotify();
    }

    /// <summary>
    /// Swaps the display order of two siblings in a single save + Changed notification.
    /// Used by Move Up / Move Down so the list rebuilds only once.
    /// </summary>
    public void SwapOrder(string idA, string idB)
    {
        var a = FindById(idA);
        var b = FindById(idB);
        if (a == null || b == null) return;

        int orderA = a.Order;
        SetItem(a with { Order = b.Order });
        SetItem(b with { Order = orderA });
        PersistAndNotify();
    }

    public void Move(string id, string? newParentId, int newOrder)
    {
        var item = FindById(id);
        if (item == null)
            return;

        newParentId = NormalizeParentId(newParentId);
        if (item.IsFolder && (string.Equals(id, newParentId, StringComparison.OrdinalIgnoreCase) || IsDescendantParent(newParentId, id)))
            return;

        string? oldParentId = item.ParentId;
        var oldSiblings = GetChildren(oldParentId).Where(child => !string.Equals(child.Id, id, StringComparison.OrdinalIgnoreCase)).ToList();
        var targetSiblings = GetChildren(newParentId).Where(child => !string.Equals(child.Id, id, StringComparison.OrdinalIgnoreCase)).ToList();
        int insertIndex = Math.Clamp(newOrder, 0, targetSiblings.Count);
        targetSiblings.Insert(insertIndex, item with { ParentId = newParentId });

        ApplySiblingOrder(newParentId, targetSiblings);

        if (!string.Equals(oldParentId, newParentId, StringComparison.OrdinalIgnoreCase))
            ApplySiblingOrder(oldParentId, oldSiblings);

        PersistAndNotify();
    }

    public bool ImportFromBrowser(string source)
    {
        string? bookmarksFile = FindBookmarksFile(source);
        if (bookmarksFile == null)
            return false;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(bookmarksFile));
            if (!doc.RootElement.TryGetProperty("roots", out var roots) ||
                !roots.TryGetProperty("bookmark_bar", out var bookmarkBar) ||
                !bookmarkBar.TryGetProperty("children", out var children))
            {
                return true;
            }

            var seenUrls = new HashSet<string>(
                Items
                    .Where(item => !item.IsFolder && !string.IsNullOrWhiteSpace(item.Url))
                    .Select(item => BuildDuplicateKey(item.Name, item.Url!)),
                StringComparer.OrdinalIgnoreCase);

            ImportChildren(children, null, seenUrls);
            PersistAndNotify();
        }
        catch
        {
            return true;
        }

        return true;
    }

    public List<FavItem> GetChildren(string? parentId)
    {
        return Items
            .Where(item => string.Equals(item.ParentId, parentId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Order)
            .ToList();
    }

    public List<FavItem> GetRoots()
    {
        return GetChildren(null);
    }

    /// <summary>Returns the first non-folder item whose URL matches, or null.</summary>
    public FavItem? FindByUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || url == "about:blank") return null;
        return Items.FirstOrDefault(i =>
            !i.IsFolder &&
            string.Equals(i.Url?.TrimEnd('/'), url.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
    }

    private void ImportChildren(JsonElement children, string? parentId, HashSet<string> seenUrls)
    {
        foreach (var child in children.EnumerateArray())
        {
            string type = child.TryGetProperty("type", out var typeElement) ? typeElement.GetString() ?? "" : "";
            string name = child.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "" : "";
            name = SanitizeName(name);

            if (string.Equals(type, "folder", StringComparison.OrdinalIgnoreCase))
            {
                string? folderId = FindFolderId(parentId, name);
                if (folderId == null)
                    folderId = AddFolderCore(name, parentId).Id;

                if (child.TryGetProperty("children", out var folderChildren))
                    ImportChildren(folderChildren, folderId, seenUrls);
            }
            else if (string.Equals(type, "url", StringComparison.OrdinalIgnoreCase))
            {
                string url = child.TryGetProperty("url", out var urlElement) ? urlElement.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                string duplicateKey = BuildDuplicateKey(name, url);
                if (!seenUrls.Add(duplicateKey))
                    continue;

                AddUrlCore(name, url, parentId);
            }
        }
    }

    private FavItem AddUrlCore(string name, string url, string? parentId)
    {
        var item = new FavItem(
            Guid.NewGuid().ToString(),
            SanitizeName(name),
            url.Trim(),
            NormalizeParentId(parentId),
            GetNextOrder(parentId),
            false);

        Items.Add(item);
        return item;
    }

    private FavItem AddFolderCore(string name, string? parentId)
    {
        var item = new FavItem(
            Guid.NewGuid().ToString(),
            SanitizeName(name),
            null,
            NormalizeParentId(parentId),
            GetNextOrder(parentId),
            true);

        Items.Add(item);
        return item;
    }

    private void CollectDescendantIds(string parentId, HashSet<string> ids)
    {
        foreach (var child in Items.Where(item => string.Equals(item.ParentId, parentId, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            if (ids.Add(child.Id) && child.IsFolder)
                CollectDescendantIds(child.Id, ids);
        }
    }

    private void PersistAndNotify()
    {
        NormalizeAllOrders();
        Save();
        Changed?.Invoke();
    }

    private FavItem? FindById(string id)
    {
        return Items.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private void SetItem(FavItem updated)
    {
        int index = Items.FindIndex(item => string.Equals(item.Id, updated.Id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            Items[index] = updated;
    }

    private void ApplySiblingOrder(string? parentId, List<FavItem> siblings)
    {
        for (int i = 0; i < siblings.Count; i++)
            SetItem(siblings[i] with { ParentId = parentId, Order = i });
    }

    private void NormalizeAllOrders()
    {
        var parents = Items.Select(item => item.ParentId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (string? parentId in parents)
        {
            var siblings = Items
                .Select((item, index) => new { item, index })
                .Where(x => string.Equals(x.item.ParentId, parentId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.item.Order)
                .ThenBy(x => x.index)
                .Select(x => x.item)
                .ToList();

            for (int i = 0; i < siblings.Count; i++)
                SetItem(siblings[i] with { Order = i });
        }
    }

    private int GetNextOrder(string? parentId)
    {
        var siblings = Items.Where(item => string.Equals(item.ParentId, parentId, StringComparison.OrdinalIgnoreCase)).ToList();
        return siblings.Count == 0 ? 0 : siblings.Max(item => item.Order) + 1;
    }

    private string? NormalizeParentId(string? parentId)
    {
        if (string.IsNullOrWhiteSpace(parentId))
            return null;

        var parent = FindById(parentId);
        return parent is { IsFolder: true } ? parent.Id : null;
    }

    private bool IsDescendantParent(string? candidateParentId, string folderId)
    {
        string? currentParentId = candidateParentId;
        while (!string.IsNullOrEmpty(currentParentId))
        {
            if (string.Equals(currentParentId, folderId, StringComparison.OrdinalIgnoreCase))
                return true;

            currentParentId = FindById(currentParentId)?.ParentId;
        }

        return false;
    }

    private string? FindFolderId(string? parentId, string name)
    {
        return Items.FirstOrDefault(item =>
            item.IsFolder &&
            string.Equals(item.ParentId, parentId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    private static string BuildDuplicateKey(string name, string url)
    {
        return $"{name.Trim()}\u001F{url.Trim()}";
    }

    private static string SanitizeName(string name)
    {
        string trimmed = name.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? "(no name)" : trimmed;
    }

    private static string? FindBookmarksFile(string source)
    {
        string[] candidatePaths = string.Equals(source, "chrome", StringComparison.OrdinalIgnoreCase)
            ? [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data", "Default", "Bookmarks")
              ]
            : [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "User Data", "Default", "Bookmarks"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge Beta", "User Data", "Default", "Bookmarks")
              ];

        return candidatePaths.FirstOrDefault(File.Exists);
    }
}
