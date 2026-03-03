using FixTrading.Common.Dtos.Instrument;
using FixTrading.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FixTrading.Persistence.Repositories;

//Instrument veritabanı işlemlerini yönetir. Tabloya yeni enstrüman ekleme, var olanı güncelleme, silme ve listeleme işlemlerini sağlar.
public class InstrumentRepository : IInstrumentRepository
{
    private readonly AppDbContext _context;    //EF core üzerinden veritabanı işlemleri için kullanılan DbContext


    //AppDbContext'i constructor üzerinden alır ve _context alanına atar. Bu sayede diğer metotlarda veritabanı işlemleri için kullanılabilir.
    public InstrumentRepository(AppDbContext context)
    {
        _context = context;
    }


    //Veritabanına yeni bir enstrüman ekler. DtoInstrument nesnesini alır, veritabanına ekler ve değişiklikleri kaydeder.
    public async Task InsertAsync(DtoInstrument dto)
    {
        await _context.Instruments.AddAsync(dto);
        await _context.SaveChangesAsync();
    }


    //Veritabanından belirli bir enstrümanı ID'sine göre getirir. ID'ye sahip enstrüman bulunamazsa null döner.
    public async Task<DtoInstrument?> FetchByIdAsync(Guid id)
    {
        return await _context.Instruments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }


    //Veritabanındaki tüm enstrümanları liste olarak getirir. AsNoTracking kullanarak performansı artırır, çünkü sadece okuma işlemi yapılır.
    public async Task<List<DtoInstrument>> FetchAllAsync()
    {
        return await _context.Instruments
            .AsNoTracking()
            .ToListAsync();
    }


    //Mevcut kaydı güncelleme
    public async Task UpdateExistingAsync(Guid id, DtoInstrument dto)
    {
        //Güncellenecek mevcut kaydı veritabanından bulur
        var existing = await _context.Instruments.FirstOrDefaultAsync(x => x.Id == id);
        if (existing != null)
        {
            dto.Id = id;
            _context.Entry(existing).CurrentValues.SetValues(dto);  //Mevcut kaydın değerlerini DTO'dan gelen yeni değerlerle günceller
            await _context.SaveChangesAsync();  //Güncelleme işlemi veritabanına yansıtılır
        }
    }

    //İd ye göre kayıt silme
    public async Task RemoveByIdAsync(Guid id)
    {
        //silinecek kaydı bulur
        var existing = await _context.Instruments.FirstOrDefaultAsync(x => x.Id == id);
        if (existing != null)
        {
            _context.Instruments.Remove(existing);
            await _context.SaveChangesAsync();   //silme işlemi veritabanına yansıtılır
        }
    }
}
