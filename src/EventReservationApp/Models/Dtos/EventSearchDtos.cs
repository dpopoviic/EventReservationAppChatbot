namespace EventReservationApp.Models.Dtos
{
    public class EventSearchFilter
    {
        public string? SearchTerm { get; set; }
        public DateTime? StartDateFrom { get; set; }
        public DateTime? StartDateTo { get; set; }
        public string? Location { get; set; }
        public bool? OnlyAvailable { get; set; }

    }

    public class EventSearchResultDto
    {
        public int EventId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int Capacity { get; set; }
        public int ReservedCount { get; set; }
        public int AvailablePlaces { get; set; }
        public bool IsFull { get; set; }
        public bool CurrentUserHasReservation { get; set; }
    }
}
