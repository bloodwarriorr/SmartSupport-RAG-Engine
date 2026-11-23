using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartSupport.Application.Chat;
using SmartSupport.Application.Documents;
using SmartSupport.Application.Feedback;
using SmartSupport.Application.Organizations;

namespace SmartSupport.Application;

public static class DependencyInjection
{
    /// <summary>Registers the business-logic layer. All services are scoped to the HTTP request.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));

        services.AddScoped<IOrganizationAccess, OrganizationAccess>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IKnowledgeSearch, KnowledgeSearch>();
        services.AddScoped<IAnswerGenerator, AnswerGenerator>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IFeedbackReportService, FeedbackReportService>();

        return services;
    }
}
