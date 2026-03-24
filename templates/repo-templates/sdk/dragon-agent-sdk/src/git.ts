export interface DragonGit {
  cloneRepo(repo: string): Promise<string>;
  createBranch(name: string): Promise<void>;
  commit(message: string): Promise<void>;
  push(): Promise<void>;
  createPullRequest(title: string, body?: string): Promise<void>;
}

export function createGitUtilities(): DragonGit {
  return {
    async cloneRepo(repo) {
      return repo;
    },
    async createBranch(_name) {},
    async commit(_message) {},
    async push() {},
    async createPullRequest(_title, _body) {}
  };
}