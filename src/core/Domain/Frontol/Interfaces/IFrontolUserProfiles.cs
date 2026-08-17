using CSharpFunctionalExtensions;
using Domain.Frontol.Models.Settings;

namespace Domain.Frontol.Interfaces;

public interface IFrontolUserProfiles {
    Task<Result<List<UserProfile>>> GetUserProfiles();
    Task<Result> LoadUserProfiles(List<UserProfile> userProfiles);
}