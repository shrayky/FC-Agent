using CSharpFunctionalExtensions;
using Domain.Frontol.Dto;

public interface IFrontolUserProfiles {
    Task<Result<List<UserProfile>>> GetUserProfiles();
    Task<Result> LoadUserProfiles(List<UserProfile> userProfiles);
}