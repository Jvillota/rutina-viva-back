using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RutaViva.Application.Abstractions;
using RutaViva.Application.Abstractions.Persistence;
using RutaViva.Application.Abstractions.Time;
using RutaViva.Application.Common;
using RutaViva.Application.Fleet;
using RutaViva.Application.Notifications;
using RutaViva.Application.Routines;
using RutaViva.Application.ServiceOrders;
using RutaViva.Application.SystemClock;
using RutaViva.Application.Telemetry;
using RutaViva.Domain.ServiceOrders;

namespace RutaViva.Application;

public static class RegisterApplicationDI
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BusinessOptions>()
            .Bind(configuration.GetSection(BusinessOptions.SectionName))
            .Validate(o => o.QuoteApprovalThresholdCop > 0, "Business:QuoteApprovalThresholdCop must be greater than zero.")
            .Validate(o => o.UtcOffsetHours is >= -12 and <= 14, "Business:UtcOffsetHours must be a valid UTC offset.")
            .ValidateOnStart();

        services.AddSingleton<IBusinessClock, BusinessClock>();
        services.AddSingleton(sp => new QuoteApprovalPolicy(sp.GetRequiredService<IOptions<BusinessOptions>>().Value.QuoteApprovalThresholdCop));

        services.AddScoped<ConcurrencyRetryExecutor>();
        services.AddScoped<OverdueRoutineOrderOpener>();

        // Fleet
        services.AddScoped<IUseCase<RegisterWorkshopRequest, WorkshopView>, RegisterWorkshopUseCase>();
        services.AddScoped<IUseCase<ListWorkshopsRequest, IReadOnlyList<WorkshopView>>, ListWorkshopsUseCase>();
        services.AddScoped<IUseCase<RegisterVehicleRequest, VehicleDetailView>, RegisterVehicleUseCase>();
        services.AddScoped<IUseCase<AddRoutineRequest, RoutineView>, AddRoutineUseCase>();
        services.AddScoped<IUseCase<ListVehiclesRequest, IReadOnlyList<VehicleSummaryView>>, ListVehiclesUseCase>();
        services.AddScoped<IUseCase<GetVehicleDetailRequest, VehicleDetailView>, GetVehicleDetailUseCase>();

        // Telemetry
        services.AddScoped<IUseCase<RegisterOdometerReadingRequest, OdometerReadingResult>, RegisterOdometerReadingUseCase>();
        services.AddScoped<IUseCase<ListOdometerReadingsRequest, IReadOnlyList<OdometerReadingEntry>>, ListOdometerReadingsUseCase>();

        // Routines
        services.AddScoped<IUseCase<RunDailyCheckRequest, DailyCheckSummary>, RunDailyCheckUseCase>();
        services.AddScoped<IUseCase<GetLastDailyCheckRequest, DateOnly?>, GetLastDailyCheckUseCase>();

        // Service orders
        services.AddScoped<IUseCase<SubmitQuoteRequest, ServiceOrderView>, SubmitQuoteUseCase>();
        services.AddScoped<IUseCase<ApproveQuoteRequest, ServiceOrderView>, ApproveQuoteUseCase>();
        services.AddScoped<IUseCase<RejectQuoteRequest, ServiceOrderView>, RejectQuoteUseCase>();
        services.AddScoped<IUseCase<CancelServiceOrderRequest, ServiceOrderView>, CancelServiceOrderUseCase>();
        services.AddScoped<IUseCase<StartServiceRequest, ServiceOrderView>, StartServiceUseCase>();
        services.AddScoped<IUseCase<CompleteServiceRequest, ServiceOrderView>, CompleteServiceUseCase>();
        services.AddScoped<IUseCase<ListServiceOrdersRequest, IReadOnlyList<ServiceOrderView>>, ListServiceOrdersUseCase>();
        services.AddScoped<IUseCase<GetServiceOrderRequest, ServiceOrderView>, GetServiceOrderUseCase>();

        // Notifications and demo clock
        services.AddScoped<IUseCase<ListNotificationsRequest, IReadOnlyList<NotificationEntry>>, ListNotificationsUseCase>();
        services.AddScoped<IUseCase<MarkNotificationReadRequest, bool>, MarkNotificationReadUseCase>();
        services.AddScoped<IUseCase<GetClockRequest, ClockView>, GetClockUseCase>();
        services.AddScoped<IUseCase<AdvanceClockRequest, ClockView>, AdvanceClockUseCase>();

        return services;
    }
}
