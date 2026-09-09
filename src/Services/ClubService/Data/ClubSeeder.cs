using ClubService.Models;
using Microsoft.Extensions.Logging;

namespace ClubService.Data;

public static class ClubSeeder
{
    public static Task SeedAsync(ClubDbContext db, ILogger? logger = null)
    {
        // Skip seeding when running against InMemory test database
        if (db.Database.ProviderName?.Contains("InMemory") == true)
            return Task.CompletedTask;

        UpdateDemoClubs(db, logger);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Updates existing demo clubs with realistic Vietnamese names, descriptions and contact info.
    /// Preserves existing IDs, memberships, manager assignments, and timestamps.
    /// Safe to re-run — idempotent (only updates, never inserts or deletes clubs).
    /// </summary>
    private static void UpdateDemoClubs(ClubDbContext db, ILogger? logger)
    {
        var clubs = db.Clubs.Where(c => c.DeletedAtUtc == null).ToList();

        foreach (var club in clubs)
        {
            var (name, shortName, category, description, email) = club.Code.ToUpperInvariant() switch
            {
                var code when code.Contains("IT") || code.Contains("TECH") || code.Contains("INFORMATION") =>
                    ("Câu lạc bộ Công nghệ Thông tin FPTU", "FPTU IT Club", ClubCategories.Academic,
                     "Câu lạc bộ dành cho sinh viên yêu thích công nghệ thông tin, lập trình, hệ thống phần mềm, an toàn thông tin và các nền tảng công nghệ mới. Câu lạc bộ tổ chức workshop, seminar, cuộc thi lập trình, hoạt động chia sẻ kiến thức và các dự án thực tế nhằm hỗ trợ sinh viên phát triển kỹ năng chuyên môn và kỹ năng làm việc nhóm.",
                     "itclub@fpt.edu.vn"),

                var code when code.Contains("SE") || code.Contains("SOFTWARE") || code.Contains("ENGINEERING") =>
                    ("Câu lạc bộ Kỹ thuật Phần mềm FPTU", "FPTU Software Engineering Club", ClubCategories.Academic,
                     "Cộng đồng sinh viên yêu thích phát triển phần mềm, kiến trúc hệ thống, microservices, kiểm thử, DevOps và quản lý dự án phần mềm. Câu lạc bộ hướng đến việc kết nối kiến thức trên lớp với quy trình phát triển sản phẩm thực tế thông qua workshop, dự án nhóm và hoạt động cố vấn chuyên môn.",
                     "seclub@fpt.edu.vn"),

                var code when code.Contains("AI") || code.Contains("DATA") || code.Contains("KHOA") =>
                    ("Câu lạc bộ Trí tuệ Nhân tạo và Khoa học Dữ liệu FPTU", "FPTU AI & Data Science Club", ClubCategories.Academic,
                     "Câu lạc bộ dành cho sinh viên quan tâm đến trí tuệ nhân tạo, học máy, khoa học dữ liệu, xử lý ngôn ngữ tự nhiên và thị giác máy tính. Hoạt động của câu lạc bộ bao gồm seminar chuyên đề, nghiên cứu nhóm, cuộc thi phân tích dữ liệu và xây dựng các sản phẩm AI phục vụ học tập và đời sống sinh viên.",
                     "aidata@fpt.edu.vn"),

                var code when code.Contains("ART") || code.Contains("MUSIC") || code.Contains("ACOUSTIC") || code.Contains("NHAC") =>
                    ("Câu lạc bộ Âm nhạc và Nghệ thuật Biểu diễn FPTU", "FPTU Music & Performing Arts Club", ClubCategories.Arts,
                     "Câu lạc bộ dành cho sinh viên yêu thích âm nhạc, thanh nhạc, nhạc cụ, biểu diễn sân khấu và tổ chức chương trình nghệ thuật. Câu lạc bộ tạo môi trường rèn luyện kỹ năng biểu diễn, làm việc nhóm, tổ chức sự kiện và góp phần xây dựng đời sống văn hóa trong trường.",
                     "musicclub@fpt.edu.vn"),

                _ => (club.Name, club.Name, club.Category, club.Description, club.ContactEmail)
            };

            club.Name = name;
            club.Category = category;
            club.Description = description;
            club.ContactEmail = email;
        }

        if (clubs.Count > 0)
        {
            db.SaveChanges();
            logger?.LogInformation("ClubSeeder: Updated {Count} demo clubs with realistic data", clubs.Count);
        }
        else
        {
            logger?.LogInformation("ClubSeeder: No clubs found to update (clubs created via application workflow)");
        }
    }
}
