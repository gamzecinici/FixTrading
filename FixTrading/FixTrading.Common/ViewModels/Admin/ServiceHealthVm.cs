namespace FixTrading.Common.ViewModels.Admin;

// Servislerin saglik durumunu gostermek icin kullanilan ViewModel.
public class ServiceHealthVm
{
    public string Name { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
}
