using FluentAssertions;
using InMoment.API.Modules.Reports;
using InMoment.Application.Features.Reports.AppealReport;
using InMoment.Application.Features.Reports.Common;
using InMoment.Application.Features.Reports.CreateReport;
using InMoment.Application.Features.Reports.GetReportDetails;
using InMoment.Application.Features.Reports.ListAllReports;
using InMoment.Application.Features.Reports.ListMyReports;
using InMoment.Application.Features.Reports.ReviewReport;
using InMoment.Domain.Reports;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InMoment.Tests.IntegrationTests.Reports;

public sealed class ReportsControllerTests
{
    private readonly Mock<IMediator> _mediator = new();

    private ReportsController Create()
        => new(_mediator.Object);

    [Fact]
    public async Task Create_ShouldSendCommand_AndReturnOkWithId()
    {
        var reportId = Guid.NewGuid();
        var request = new ReportsController.CreateReportRequest(
            ReportTargetType.Photo,
            Guid.NewGuid(),
            ReportReason.Spam,
            "details");

        _mediator.Setup(x => x.Send(
                It.Is<CreateReportCommand>(c =>
                    c.TargetType == request.TargetType &&
                    c.TargetId == request.TargetId &&
                    c.Reason == request.Reason &&
                    c.Description == request.Description),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportId);

        var controller = Create();

        var result = await controller.Create(request, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(reportId);
    }

    [Fact]
    public async Task My_ShouldReturnOk_WithMediatorResult()
    {
        IReadOnlyList<ReportDto> expected = Array.Empty<ReportDto>();

        _mediator.Setup(x => x.Send(
                It.Is<ListMyReportsQuery>(q => q.Limit == 25),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.My(25, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task All_ShouldReturnOk_WithMediatorResult()
    {
        IReadOnlyList<ReportListItemDto> expected = Array.Empty<ReportListItemDto>();

        _mediator.Setup(x => x.Send(
                It.Is<ListAllReportsQuery>(q => q.Limit == 75),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.All(75, null, null, null, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Details_ShouldReturnOk_WithMediatorResult()
    {
        var reportId = Guid.NewGuid();

        var expected = new ReportDetailsDto(
            Id: reportId,
            ReporterUserId: Guid.NewGuid(),
            TargetType: ReportTargetType.Photo,
            TargetId: Guid.NewGuid(),
            Reason: ReportReason.Spam,
            Description: "details",
            Status: ReportStatus.Pending,
            ReviewedByUserId: null,
            ReviewedAt: null,
            CreatedAt: DateTime.UtcNow,
            Reporter: null,
            TargetContext: new ReportTargetContextDto(null, null, null),
            Resolution: new ReportResolutionInfoDto(
                IsResolved: false,
                ResolutionCode: null,
                ResolutionText: "Жалоба отправлена и ожидает проверки модератором.",
                AppealText: null,
                AppealedAt: null)
        );

        _mediator.Setup(x => x.Send(
                It.Is<GetReportDetailsQuery>(q => q.ReportId == reportId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = Create();

        var result = await controller.Details(reportId, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Review_ShouldSendCommand_AndReturnOkWithId()
    {
        var reportId = Guid.NewGuid();
        var reviewedId = Guid.NewGuid();
        var request = new ReportsController.ReviewReportRequest(
            ReportStatus.Rejected,
            ReviewReportAction.None);

        _mediator.Setup(x => x.Send(
                It.Is<ReviewReportCommand>(c =>
                    c.ReportId == reportId &&
                    c.Status == ReportStatus.Rejected &&
                    c.Action == ReviewReportAction.None),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(reviewedId);

        var controller = Create();

        var result = await controller.Review(reportId, request, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(reviewedId);
    }

    [Fact]
    public async Task Appeal_ShouldSendCommand_AndReturnOkWithId()
    {
        var reportId = Guid.NewGuid();
        var appealedId = Guid.NewGuid();
        var request = new ReportsController.AppealRequest("please review again");

        _mediator.Setup(x => x.Send(
                It.Is<AppealReportCommand>(c =>
                    c.ReportId == reportId &&
                    c.Text == "please review again"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(appealedId);

        var controller = Create();

        var result = await controller.Appeal(reportId, request, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(appealedId);
    }
}