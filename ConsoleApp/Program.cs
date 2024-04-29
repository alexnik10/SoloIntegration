using Demands;
using Demands.Contracts;
using Demands.DTO;
using Demands.DTO.Command;
using Demands.DTO.DocumentDetails;
using Demands.DTO.Employee;
using Demands.Ussc.DTO;
using Demands.Ussc.DTO.Commands;
using OrgStructure;
using OrgStructure.Model;
using WellKnown = Demands.Ussc.DTO.WellKnown;

var client = UsscDemandsApi.Build(x =>
    x.ConnectToHost("https://solo-demo.ft-soft.ru/")
        .SetApplicationToken("EducationPortal")
        .UseHttpClient());
await OrgStructureApi.Start(x => x.LoadFromFile("organization.json"));
var org = OrgStructureApi.GetOrganization();

var context = client.GetContext();

//Имперсонируемся за пользователя и устанавливаем профиль, из под которого будем выполнять действия
await ImpersonateAndSetProfile(context);

//var trainingRequestId = Guid.NewGuid();
//Создаём и отправляем на согласование заявку на внешнее обучение
//await CreateTrainingRequest(trainingRequestId, context, org);

//Получаем информацию о созданном документе
//var trainingRequest = await context.Documents.GetDetails(trainingRequestId);

//Подтверждаем запись на курс обучения
//await ConfirmTrainingRequestEnrollment(trainingRequest, context);

// Получаем первые 20 документов в системе
var allDocuments = await context.Documents.Search(new Demands.DTO.Search.DocumentSearchOptionsApiDTO());
Console.WriteLine("Документы:");
foreach (var item in allDocuments)
{
    Console.WriteLine(item.Title + ": " + item.Author.LastName);
}

// Получаем //     Список всех сотрудников, отсортированный по полному имени.
var people = org.People.OrderedByFullName;
foreach (var item in people)
{
    Console.WriteLine(item.Id + ": " + item.GetFullName());
}

// Получаем сотрудника по email.
var yakovleva = org.People.FirstOrDefault(x => x.Email == "YakovlevaKA@example.ru");
Console.WriteLine(yakovleva.GetFullName());
foreach (var item in yakovleva.GetAllStaffPositions())
{
    Console.WriteLine(item.PostName);
}
return;

static async Task ImpersonateAndSetProfile(IDemandsApiContext context)
{
    var VasilievaRVId = new Guid("67c6ee3c-d0e4-0266-3b6b-4c591a578aa7");
    context.Impersonate(VasilievaRVId);
    
    var profiles = await context.Profiles.GetProfiles();
    context.SetProfile(profiles.First(x => x.Type == ProfileType.Personal));
}

static async Task CreateTrainingRequest(Guid trainingRequestId, IDemandsApiContext context, Organization org)
{
    var createCommand = await context.Documents.GetBlank(WellKnown.TrainingRequest.DocumentTypeId);
    createCommand.DocumentId = trainingRequestId;

    //Заполняем кастомные поля документа
    FillTrainingRequestFields(createCommand, org);

    var sendCommand = new SendToApprovementDemandCommandApiDTO
    {
        CreateCommand = createCommand,
        DocumentId = createCommand.DocumentId,
        ProfileId = createCommand.ProfileId
    };
    await context.Documents.ExecuteCommand(sendCommand);
}

static void FillTrainingRequestFields(CreateDemandCommandApiDTO createCommand, Organization org)
{
    var orgUnit = org.OrgUnits.First(x => x.Chief.Appointment != null);
    var initiator = org.People.Where(x => !x.IsFired()).Take(1).Single();
    var participants = org.People.Where(x => !x.IsFired()).Skip(1).Take(5).ToList();

    createCommand.OuterApproversAppointmentsIds = [orgUnit.Chief.Appointment.Id];
    
    createCommand.CustomFields[WellKnown.TrainingRequest.TrainingGoal] = "У самурая нет цели только путь";
    createCommand.CustomFields[WellKnown.TrainingRequest.TrainingCourse] = new TrainingCourseApiDTO
    {
        Name = "Искусство Хокку",
        Type = "Очные курсы",
        Category = "Soft skill",
        Description = "С треском лопнул кувшин:\nНочью вода в нем замерзла.\nЯ пробудился вдруг.",
        TrainingCenter = "Центр японской культуры",
        Cost = "500 руб.",
    };
    createCommand.CustomFields[WellKnown.TrainingRequest.Initiator] = initiator.Id;
    createCommand.CustomFields[WellKnown.TrainingRequest.Participants] = participants.Select(x=>x.Id).ToList();
    createCommand.CustomFields[WellKnown.TrainingRequest.Period] = new PeriodApiDTO
    {
        StartDate = DateTime.Today,
        FinishDate = DateTime.Today.AddMonths(1)
    };
}

static async Task ConfirmTrainingRequestEnrollment(DocumentDetailsApiDTO trainingRequest, IDemandsApiContext context)
{
    var confirmEnrollmentCommand = trainingRequest.Commands
        .OfType<ConfirmEnrollmentTrainingRequestCommandApiDTO>()
        .SingleOrDefault();

    if (confirmEnrollmentCommand != null)
        await context.Documents.ExecuteCommand(confirmEnrollmentCommand);
}