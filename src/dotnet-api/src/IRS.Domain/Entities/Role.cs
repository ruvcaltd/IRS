using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IRS.Infrastructure;

[Index("name", Name = "UQ__Roles__72E12F1B2A4B0C8E", IsUnique = true)]
public partial class Role
{
    [Key]
    public int id { get; set; }

    [StringLength(50)]
    public string name { get; set; } = null!;

    [InverseProperty("role")]
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
