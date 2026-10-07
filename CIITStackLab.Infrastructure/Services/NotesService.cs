using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class NotesService : INotesService
{
    private readonly ApplicationDbContext _dbContext;
    public NotesService(ApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<AdminNoteDto>> GetAdminIndexAsync(CancellationToken cancellationToken = default) =>
        await (from note in _dbContext.TrainingNotes.AsNoTracking()
               join course in _dbContext.Courses.AsNoTracking() on note.CourseId equals course.Id
               join topic in _dbContext.Topics.AsNoTracking() on note.TopicId equals topic.Id
               where note.Flag == 0 && course.Flag == 0 && topic.Flag == 0
               orderby course.Title, topic.Title, note.SortOrder, note.Id
               select new AdminNoteDto { Id=note.Id, CourseId=note.CourseId, CourseTitle=course.Title, TopicId=note.TopicId, TopicTitle=topic.Title, PageId=note.PageId, Title=note.Title, TitleWithNumber=note.TitleWithNumber, HtmlContent=note.HtmlContent, SortOrder=note.SortOrder, UpdatedAt=note.UpdatedAt, CreatedAt=note.CreatedAt }).ToListAsync(cancellationToken);

    public async Task<AdminNoteDto?> GetAdminByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await (from note in _dbContext.TrainingNotes.AsNoTracking()
               join course in _dbContext.Courses.AsNoTracking() on note.CourseId equals course.Id
               join topic in _dbContext.Topics.AsNoTracking() on note.TopicId equals topic.Id
               where note.Id == id && note.Flag == 0 && course.Flag == 0 && topic.Flag == 0
               select new AdminNoteDto { Id=note.Id, CourseId=note.CourseId, CourseTitle=course.Title, TopicId=note.TopicId, TopicTitle=topic.Title, PageId=note.PageId, Title=note.Title, TitleWithNumber=note.TitleWithNumber, HtmlContent=note.HtmlContent, SortOrder=note.SortOrder, UpdatedAt=note.UpdatedAt, CreatedAt=note.CreatedAt }).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<NoteCourseTopicOptionDto>> GetCourseTopicOptionsAsync(CancellationToken cancellationToken = default) =>
        await (from mapping in _dbContext.CourseModules.AsNoTracking()
               join course in _dbContext.Courses.AsNoTracking() on mapping.CourseId equals course.Id
               join topic in _dbContext.Topics.AsNoTracking() on mapping.TopicId equals topic.Id
               where mapping.Flag == 0 && course.Flag == 0 && topic.Flag == 0
               orderby course.Title, topic.Title
               select new NoteCourseTopicOptionDto { CourseId=course.Id, CourseTitle=course.Title, TopicId=topic.Id, TopicTitle=topic.Title }).Distinct().ToListAsync(cancellationToken);

    public async Task<(bool Succeeded, string? Error)> CreateAsync(AdminNoteInputDto input, CancellationToken cancellationToken = default)
    {
        var error=await ValidateTargetAsync(input.CourseId,input.TopicId,input.PageId,null,cancellationToken); if(error is not null)return(false,error);
        var note=new Domain.Entities.TrainingNote { CourseId=input.CourseId,TopicId=input.TopicId,PageId=input.PageId.Trim(),Title=input.Title.Trim(),TitleWithNumber=input.TitleWithNumber.Trim(),HtmlContent=input.HtmlContent.Trim(),SortOrder=input.SortOrder,Flag=0,CreatedAt=DateTime.Now,UpdatedAt=DateTime.Now };
        _dbContext.TrainingNotes.Add(note); await _dbContext.SaveChangesAsync(cancellationToken); return(true,null);
    }

    public async Task<(bool Succeeded, string? Error)> UpdateAsync(int id, AdminNoteInputDto input, CancellationToken cancellationToken = default)
    {
        var note=await _dbContext.TrainingNotes.SingleOrDefaultAsync(x=>x.Id==id&&x.Flag==0,cancellationToken); if(note is null)return(false,"The selected note was not found or is inactive.");
        var error=await ValidateTargetAsync(input.CourseId,input.TopicId,input.PageId,id,cancellationToken); if(error is not null)return(false,error);
        note.CourseId=input.CourseId; note.TopicId=input.TopicId; note.PageId=input.PageId.Trim(); note.Title=input.Title.Trim(); note.TitleWithNumber=input.TitleWithNumber.Trim(); note.HtmlContent=input.HtmlContent.Trim(); note.SortOrder=input.SortOrder; note.UpdatedAt=DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken); return(true,null);
    }

    public async Task<(bool Succeeded, string? Error)> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var note=await _dbContext.TrainingNotes.SingleOrDefaultAsync(x=>x.Id==id&&x.Flag==0,cancellationToken); if(note is null)return(false,"The selected note was not found or is inactive.");
        note.Flag=1; note.DeletedAt=DateTime.Now; note.UpdatedAt=DateTime.Now; await _dbContext.SaveChangesAsync(cancellationToken); return(true,null);
    }

    public async Task<CourseNotesDto?> GetReaderAsync(int courseId,int topicId,string? pageId,CancellationToken cancellationToken=default)
    {
        var target=await (from mapping in _dbContext.CourseModules.AsNoTracking()
                          join course in _dbContext.Courses.AsNoTracking() on mapping.CourseId equals course.Id
                          join topic in _dbContext.Topics.AsNoTracking() on mapping.TopicId equals topic.Id
                          where mapping.CourseId==courseId&&mapping.TopicId==topicId&&mapping.Flag==0&&course.Flag==0&&topic.Flag==0
                          select new { CourseId=course.Id,CourseTitle=course.Title,TopicId=topic.Id,TopicTitle=topic.Title }).FirstOrDefaultAsync(cancellationToken);
        if(target is null)return null;
        var chapters=await _dbContext.TrainingNotes.AsNoTracking().Where(x=>x.CourseId==courseId&&x.TopicId==topicId&&x.Flag==0).OrderBy(x=>x.SortOrder).ThenBy(x=>x.Id).Select(x=>new NoteChapterDto { Id=x.Id,PageId=x.PageId,Title=x.Title,TitleWithNumber=x.TitleWithNumber,HtmlContent=x.HtmlContent,SortOrder=x.SortOrder }).ToListAsync(cancellationToken);
        if(chapters.Count==0)return null;
        var current=!string.IsNullOrWhiteSpace(pageId)?chapters.FirstOrDefault(x=>x.PageId==pageId)??chapters[0]:chapters[0];
        return new CourseNotesDto { CourseId=target.CourseId,CourseTitle=target.CourseTitle,TopicId=target.TopicId,TopicTitle=target.TopicTitle,Chapters=chapters,CurrentPageId=current.PageId,CurrentChapter=current };
    }

    private async Task<string?> ValidateTargetAsync(int courseId,int topicId,string pageId,int? excludingId,CancellationToken cancellationToken)
    {
        if(string.IsNullOrWhiteSpace(pageId))return "Page ID is required.";
        var mapped=await _dbContext.CourseModules.AnyAsync(x=>x.CourseId==courseId&&x.TopicId==topicId&&x.Flag==0,cancellationToken); if(!mapped)return "The selected topic is not mapped to the selected course.";
        var duplicate=await _dbContext.TrainingNotes.AnyAsync(x=>x.CourseId==courseId&&x.TopicId==topicId&&x.PageId==pageId.Trim()&&x.Flag==0&&(!excludingId.HasValue||x.Id!=excludingId.Value),cancellationToken);
        return duplicate?"This Page ID already exists for the selected course and topic.":null;
    }
}