using System.Threading.Tasks;
using TransactionService.Models;

namespace TransactionService.Services.Interface
{
    public interface IAccountBalanceService
    {
        Task UpdateBalanceAsync(int accountId, AccountBalanceRequest balanceRequest);
    }
}