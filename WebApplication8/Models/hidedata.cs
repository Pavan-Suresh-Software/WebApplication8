using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication8.Models
{
    public class hidedata
    {
        [NotMapped]
        public bool export { get; set; } = true;//
    }
}
