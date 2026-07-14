import React from "react";
import { render, screen } from "@testing-library/react";

vi.mock("react-i18next", () => ({ useTranslation: () => ({ t: (k: string, d?: string) => d ?? k }) }));

// antd Descriptions: render title + rows (label: value) so we can assert content.
vi.mock("antd", () => {
  const Item = ({ label, children }: any) => <div><span>{label}</span><span>{children}</span></div>;
  const Descriptions = ({ title, children }: any) => <div>{title ? <div>{title}</div> : null}{children}</div>;
  Descriptions.Item = Item;
  return { Descriptions };
});

vi.mock("../../../../../shared/components/common/StatusTag", () => ({
  StatusTag: ({ label }: any) => <span>{label}</span>,
}));
vi.mock("../../../../../shared/utils/formatDate", () => ({ formatDateTime: (d: string) => `dt(${d})` }));
vi.mock("../../../../../shared/utils/errorHandler", () => ({ translateErrorMessage: (m: string) => `err(${m})` }));

import { TaskDetailScreen } from "../TaskDetailScreen";
import { WorkflowStatus } from "../../../types/workflow.types";

const wf = (type: string, context: object, extra: object = {}) => ({
  id: "w1", type, status: WorkflowStatus.Processing, context: JSON.stringify(context),
  createdAt: "2026-07-14T00:00:00Z", ...extra,
}) as any;

describe("TaskDetailScreen — type-switched body", () => {
  it("renders the REMOTE body from the RemoteImportJob context", () => {
    render(<TaskDetailScreen workflow={wf("REMOTE_IMPORT", {
      sourceId: "gamebanana",
      detail: { title: "Vesper", detailUrl: "https://gamebanana.com/mods/1" },
      option: { type: "direct", name: "GB", url: "https://gamebanana.com/dl/1" },
      categoryId: "uncategorized", tags: ["zzz"],
    })} />);

    expect(screen.getByText("Vesper")).toBeInTheDocument();
    expect(screen.getByText("workflow.detail.typeRemote")).toBeInTheDocument();
    expect(screen.getByText("workflow.detail.remoteSection")).toBeInTheDocument();
    expect(screen.getByText("gamebanana")).toBeInTheDocument();
    expect(screen.getByText("https://gamebanana.com/dl/1")).toBeInTheDocument();
    // No local-only section.
    expect(screen.queryByText("workflow.detail.localSection")).not.toBeInTheDocument();
  });

  it("renders the LOCAL body from the ModImportWorkflowContext", () => {
    render(<TaskDetailScreen workflow={wf("MOD_IMPORT", {
      name: "My Mod", folderPath: "C:/mods/my-mod", step: "compress_folder", fileCount: 4,
    })} />);

    expect(screen.getAllByText("My Mod").length).toBeGreaterThan(0); // header title + name row
    expect(screen.getByText("workflow.detail.typeLocal")).toBeInTheDocument();
    expect(screen.getByText("workflow.detail.localSection")).toBeInTheDocument();
    expect(screen.getByText("C:/mods/my-mod")).toBeInTheDocument();
    expect(screen.queryByText("workflow.detail.remoteSection")).not.toBeInTheDocument();
  });

  it("shows the error row for a failed task", () => {
    render(<TaskDetailScreen workflow={wf("REMOTE_IMPORT", { detail: { title: "X" }, option: {} },
      { status: WorkflowStatus.Failed, errorMessage: "boom" })} />);
    expect(screen.getByText("err(boom)")).toBeInTheDocument();
  });
});
