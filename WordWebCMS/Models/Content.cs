using FreeSql.DataAnnotations;

namespace WordWebCMS.Models;

public enum PostKind { Article, Page }
public enum RevisionState { Saved, Pending, Published, Rejected }

[Table(Name = "post")]
[Index("ux_post_slug", nameof(Slug), true)]
[Index("ix_post_public", nameof(IsPublic) + "," + nameof(Kind) + "," + nameof(PublishedUtc), false)]
public class Post
{
    [Column(IsPrimary = true, IsIdentity = true)] public long Id { get; set; }
    public long AuthorId { get; set; }
    [Column(MapType = typeof(int))] public PostKind Kind { get; set; }
    // Only publishing changes this public route. The working slug lives in PostDraft.
    [Column(StringLength = 160)] public string Slug { get; set; } = Guid.NewGuid().ToString("N");
    public long? PublishedRevisionId { get; set; }
    public bool IsPublic { get; set; }
    public int PinOrder { get; set; }
    public DateTime? PublishedUtc { get; set; }
    public long Views { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

// Both persisted content versions have the same editable fields, without hidden database IO.
public class PostContent
{
    [Column(StringLength = 200)] public string Title { get; set; } = "";
    [Column(StringLength = 160)] public string Slug { get; set; } = "";
    [Column(StringLength = 1000)] public string Summary { get; set; } = "";
    [Column(StringLength = -1)] public string Markdown { get; set; } = "";
    [Column(StringLength = 1000)] public string CoverUrl { get; set; } = "";
    public long? CategoryId { get; set; }
    // The draft is a form value. Immutable revisions use RevisionTag rows for queries.
    [Column(StringLength = 1000)] public string Tags { get; set; } = "";
    public bool AllowComments { get; set; } = true;
    [Column(StringLength = -1)] public string Translations { get; set; } = "{}";
}

[Table(Name = "post_draft")]
public class PostDraft : PostContent
{
    [Column(IsPrimary = true)] public long PostId { get; set; }
    public long Version { get; set; } = 1;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

[Table(Name = "post_revision")]
[Index("ix_revision_post", nameof(PostId) + "," + nameof(Id), false)]
public class PostRevision : PostContent
{
    [Column(IsPrimary = true, IsIdentity = true)] public long Id { get; set; }
    public long PostId { get; set; }
    public long EditorId { get; set; }
    [Column(MapType = typeof(int))] public RevisionState State { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

[Table(Name = "category")]
[Index("ux_category_name", nameof(Name), true)]
public class Category
{
    [Column(IsPrimary = true, IsIdentity = true)] public long Id { get; set; }
    [Column(StringLength = 80)] public string Name { get; set; } = "";
}

[Table(Name = "tag")]
[Index("ux_tag_name", nameof(Name), true)]
public class Tag
{
    [Column(IsPrimary = true, IsIdentity = true)] public long Id { get; set; }
    [Column(StringLength = 50)] public string Name { get; set; } = "";
}

[Table(Name = "revision_tag")]
public class RevisionTag
{
    [Column(IsPrimary = true)] public long RevisionId { get; set; }
    [Column(IsPrimary = true)] public long TagId { get; set; }
}

[Table(Name = "comment")]
[Index("ix_comment_post", nameof(PostId) + "," + nameof(Approved) + "," + nameof(Id), false)]
public class Comment
{
    [Column(IsPrimary = true, IsIdentity = true)] public long Id { get; set; }
    public long PostId { get; set; }
    public long UserId { get; set; }
    public long? ParentId { get; set; }
    [Column(StringLength = 10000)] public string Markdown { get; set; } = "";
    public bool Approved { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

[Table(Name = "post_like")]
public class PostLike
{
    [Column(IsPrimary = true)] public long PostId { get; set; }
    [Column(IsPrimary = true)] public long UserId { get; set; }
}

[Table(Name = "comment_like")]
public class CommentLike
{
    [Column(IsPrimary = true)] public long CommentId { get; set; }
    [Column(IsPrimary = true)] public long UserId { get; set; }
}
