using Coldairarrow.Business.Cache;
using Coldairarrow.Entity.Base_Manage;
using Coldairarrow.Util;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Coldairarrow.Business.Base_Manage
{
    public class Base_UserBusiness : BaseBusiness<Base_User>, IBase_UserBusiness, IDependency
    {
        #region DI

        public Base_UserBusiness(IBase_UserDTOCache sysUserCache)
        {
            _sysUserCache = sysUserCache;
        }
        IBase_UserDTOCache _sysUserCache { get; }

        #endregion

        #region �ⲿ�ӿ�

        public List<Base_UserDTO> GetDataList(Pagination pagination, bool all, string userId = null, string keyword = null)
        {
            Expression<Func<Base_User, Base_Department, Base_UserDTO>> select = (a, b) => new Base_UserDTO
            {
                DepartmentName = b.Name
            };
            select = select.BuildExtendSelectExpre();
            var q_User = all ? Service.GetIQueryable<Base_User>() : GetIQueryable();
            var q = from a in q_User.AsExpandable()
                    join b in Service.GetIQueryable<Base_Department>() on a.DepartmentId equals b.Id into ab
                    from b in ab.DefaultIfEmpty()
                    select @select.Invoke(a, b);

            var where = LinqHelper.True<Base_UserDTO>();
            if (!userId.IsNullOrEmpty())
                where = where.And(x => x.Id == userId);
            if (!keyword.IsNullOrEmpty())
            {
                where = where.And(x =>
                    EF.Functions.Like(x.UserName, keyword)
                    || EF.Functions.Like(x.RealName, keyword));
            }

            var list = q.Where(where).GetPagination(pagination).ToList();

            SetProperty(list);

            return list;

            void SetProperty(List<Base_UserDTO> users)
            {
                //�����û���ɫ����
                List<string> userIds = users.Select(x => x.Id).ToList();
                var userRoles = (from a in Service.GetIQueryable<Base_UserRole>()
                                 join b in Service.GetIQueryable<Base_Role>() on a.RoleId equals b.Id
                                 where userIds.Contains(a.UserId)
                                 select new
                                 {
                                     a.UserId,
                                     RoleId = b.Id,
                                     b.RoleName
                                 }).ToList();
                users.ForEach(aUser =>
                {
                    var roleList = userRoles.Where(x => x.UserId == aUser.Id);
                    aUser.RoleIdList = roleList.Select(x => x.RoleId).ToList();
                    aUser.RoleNameList = roleList.Select(x => x.RoleName).ToList();
                });
            }
        }

        public Base_UserDTO GetTheData(string id)
        {
            if (id.IsNullOrEmpty())
                return null;
            else
                return GetDataList(new Pagination(), true, id).FirstOrDefault();
        }

        public Base_UserDTO GetTheInfo(string userId)
        {
            return _sysUserCache.GetCache(userId);
        }

        [DataAddLog(LogType.ϵͳ�û�����, "RealName", "�û�")]
        [DataRepeatValidate(
            new string[] { "UserName" },
            new string[] { "�û���" })]
        public AjaxResult AddData(Base_User newData, List<string> roleIds)
        {
            using (var transaction = BeginTransaction())
            {
                Insert(newData);
                SetUserRole(newData.Id, roleIds);

                var res = EndTransaction();
                if (res.Success)
                    return Success();
                else
                    throw new Exception("ϵͳ�쳣", res.ex);
            }
        }

        [DataEditLog(LogType.ϵͳ�û�����, "RealName", "�û�")]
        [DataRepeatValidate(
            new string[] { "UserName" },
            new string[] { "�û���" })]
        public AjaxResult UpdateData(Base_User theData, List<string> roleIds)
        {
            if (theData.Id == "Admin" && Operator?.UserId != theData.Id)
                return new ErrorResult("��ֹ���ĳ�������Ա��");

            using (var transaction = BeginTransaction())
            {
                Update(theData);
                SetUserRole(theData.Id, roleIds);

                var res = EndTransaction();
                if (res.Success)
                {
                    _sysUserCache.UpdateCache(theData.Id);
                    return Success();
                }
                else
                    throw new Exception("ϵͳ�쳣", res.ex);
            }
        }

        [DataDeleteLog(LogType.ϵͳ�û�����, "RealName", "�û�")]
        public AjaxResult DeleteData(List<string> ids)
        {
            var adminUser = GetTheInfo("Admin");
            if (ids.Contains(adminUser.Id))
                return new ErrorResult("��������Ա�������˺�,��ֹɾ����");
            var userIds = GetIQueryable().Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToList();

            Delete(ids);
            _sysUserCache.UpdateCache(userIds);

            return Success();
        }

        #endregion

        #region ˽�г�Ա

        private void SetUserRole(string userId, List<string> roleIds)
        {
            var userRoleList = roleIds.Select(x => new Base_UserRole
            {
                Id = IdHelper.GetId(),
                CreateTime = DateTime.Now,
                UserId = userId,
                RoleId = x
            }).ToList();
            Service.Delete_Sql<Base_UserRole>(x => x.UserId == userId);
            Service.Insert(userRoleList);
        }

        #endregion

        #region ����ģ��

        #endregion
    }

    [MapFrom(typeof(Base_User))]
    public class Base_UserDTO : Base_User
    {
        public string RoleNames { get => string.Join(",", RoleNameList); }
        public List<string> RoleIdList { get; set; }
        public List<string> RoleNameList { get; set; }
        public EnumType.RoleType RoleType
        {
            get
            {
                int type = 0;

                var values = typeof(EnumType.RoleType).GetEnumValues();
                foreach (var aValue in values)
                {
                    if (RoleNames.Contains(aValue.ToString()))
                        type += (int)aValue;
                }

                return (EnumType.RoleType)type;
            }
        }
        public string DepartmentName { get; set; }
        public string SexText { get => Sex == 1 ? "��" : "Ů"; }
        public string BirthdayText { get => Birthday?.ToString("yyyy-MM-dd"); }
    }
}