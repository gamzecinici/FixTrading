namespace FixTrading.Domain.Interfaces;

// Bu interface, veritabanı işlemlerini standart hale getirir
// Yani tüm CRUD işlemlerini (ekle, getir, güncelle, sil) tek bir yerde tanımlar
public interface IBaseRepository<T> where T : class
{
    // Yeni veri ekler
    Task InsertAsync(T entity);

    // Id'ye göre veri getirir (yoksa null döner)
    Task<T?> FetchByIdAsync(long id);

    // Tüm verileri listeler
    Task<List<T>> FetchAllAsync();

    // Var olan veriyi günceller
    Task UpdateExistingAsync(long id, T entity);

    // Id'ye göre veriyi siler
    Task RemoveByIdAsync(long id);
}
