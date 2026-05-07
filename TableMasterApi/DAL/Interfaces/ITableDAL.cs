using System.Collections.Generic;
using System.Threading.Tasks;
using TableMasterApi.Model;

namespace TableMasterApi.DAL.Interfaces
{
    /// <summary>
    /// Interface pour la gestion des tables
    /// </summary>
    public interface ITableDAL
    {
        Task<TableEntityOut?> GetTablesById(long tableId);
        Task<IEnumerable<TableEntityOut>> GetTablesByRestaurantAsync(long restaurantId);
        Task<TableEntityOut?> CreateTable(TableEntityIn table);
        Task<TableEntityOut?> UpdateTable(long tableId, TableEntityIn table);
        Task<bool> DeleteTable(long tableId);
        Task<IEnumerable<TableEntityOut>> ReplaceTablesAsync(long restaurantId, IEnumerable<TableEntityIn> tables);
    }
}
