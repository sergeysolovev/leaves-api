﻿using System.Linq;
using System.Net.Http;
using System.Text;
using AutoMapper;
using ABC.Leaves.Api.Enums;
using ABC.Leaves.Api.Helpers;
using ABC.Leaves.Api.Models;
using ABC.Leaves.Api.Repositories;
using ABC.Leaves.Api.Dto;
using Google.Apis.Calendar.v3.Data;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace ABC.Leaves.Api.Services
{
    public class EmployeeLeavesService : IEmployeeLeavesService
    {
        private readonly IEmployeeLeavesRepository repository;
        private readonly IEmployeeRepository employeeRepository;
        private readonly IMapper mapper;

        public EmployeeLeavesService(IEmployeeLeavesRepository repository, IEmployeeRepository employeeRepository, IMapper mapper)
        {
            this.repository = repository;
            this.employeeRepository = employeeRepository;
            this.mapper = mapper;
        }

        public IActionResult Apply(EmployeeLeaveDto employeeLeaveDto)
        {
            if (employeeLeaveDto == null)
            {
                return new BadRequestResult();
            }
            var model = mapper.Map<EmployeeLeaveDto, EmployeeLeave>(employeeLeaveDto);
            model.Status = EmployeeLeaveStatus.Applied;
            repository.Insert(model);
            return new OkResult();
        }

        public IActionResult Approve(string id)
        {
            if (id == null) return new BadRequestObjectResult("ID can not be null");
            var model = repository.Find(id);
            if (model == null) return new NotFoundResult();
            if (model.Status == EmployeeLeaveStatus.Approved) return new BadRequestObjectResult("The request was already approved.");

            var result = ChangeStatus(model, EmployeeLeaveStatus.Approved);
            if (result is OkResult)
            {
                return AddGoogleCalendarEvent(model.GoogleAuthAccessToken, model);
            }
            return result;
        }

        public IActionResult Decline(string id)
        {
            if (id == null) return new BadRequestObjectResult("ID can not be null");
            var model = repository.Find(id);
            if (model == null) return new NotFoundResult();
            if (model.Status == EmployeeLeaveStatus.Approved) return new BadRequestObjectResult("The request was approved, can not decline it.");

            return ChangeStatus(model, EmployeeLeaveStatus.Declined);
        }

        private IActionResult ChangeStatus(EmployeeLeave model, EmployeeLeaveStatus status)
        {
            model.Status = status;
            repository.Update(model);
            return new OkResult();
        }

        private static IActionResult AddGoogleCalendarEvent(string accessToken, EmployeeLeave model)
        {
            using (var client = new System.Net.Http.HttpClient())
            {
                var response = client.GetStringAsync("https://www.googleapis.com/calendar/v3/users/me/calendarList?alt=json&access_token=" + accessToken).Result;
                var calendarList = JsonConvert.DeserializeObject<CalendarList>(response);
                var calendar = calendarList.Items.FirstOrDefault(c => c.Primary != null && c.Primary.Value);

                var e = ConstructGoogleCalendarEvent(model);

                var json = JsonConvert.SerializeObject(e, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

                var httpResponse =
                    client.PostAsync(
                        "https://www.googleapis.com/calendar/v3/calendars/" + calendar.Id + "/events?alt=json&access_token=" +
                        accessToken, httpContent).Result;
                if (!httpResponse.IsSuccessStatusCode)
                {
                    var responseContent = httpResponse.Content.ReadAsStringAsync().Result;
                    return new BadRequestObjectResult(responseContent);
                }
            }
            return new OkResult();
        }

        private static Event ConstructGoogleCalendarEvent(EmployeeLeave model)
        {
            var e = new Event
            {
                Start = new EventDateTime { DateTime = model.Start },
                End = new EventDateTime { DateTime = model.End },
                Attendees = new[]
                {
                    new EventAttendee {Email = UserInfoHelper.GetUserGmailAddress(model.GoogleAuthAccessToken)}
                },
                Reminders = new Event.RemindersData
                {
                    UseDefault = false,
                    Overrides = new[]
                    {
                        new EventReminder {Method = "email", Minutes = 24*60},
                        new EventReminder {Method = "sms", Minutes = 10},
                    }
                },
                Attachments = new[]
                {
                    new EventAttachment {FileUrl = string.Empty}
                },
                Summary = "Leaving Work Space Time",
                Description = "You do not work at this time."
            };
            return e;
        }
    }
}
