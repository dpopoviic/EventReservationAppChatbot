namespace EventReservationApp.Models.Dtos
{
    public class EventAvailabilityDto
    {
        public int EventId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public int ReservedCount { get; set; }
        public int AvailablePlaces { get; set; }
        public bool IsAvailableForReservation { get; set; }

    }
}
