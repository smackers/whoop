// 
// Copyright (c) 2014 Pantazis Deligiannis (p.deligiannis@imperial.ac.uk)
// This file is distributed under the MIT License. See LICENSE for details.
// 

#include "clang/Driver/Options.h"
#include "clang/AST/AST.h"
#include "clang/AST/ASTContext.h"
#include "clang/AST/ASTConsumer.h"
#include "clang/AST/RecursiveASTVisitor.h"
#include "clang/Frontend/ASTConsumers.h"
#include "clang/Frontend/FrontendActions.h"
#include "clang/Frontend/CompilerInstance.h"
#include "clang/Tooling/CommonOptionsParser.h"
#include "clang/Tooling/Tooling.h"
#include "clang/Rewrite/Core/Rewriter.h"

using namespace std;
using namespace clang;
using namespace clang::driver;
using namespace clang::tooling;
using namespace llvm;

string FileName;
	
class DriverInfo {
private:
	map<string, map<string, string> > entry_points;
	
	DriverInfo() {}
	DriverInfo(DriverInfo const&);
	void operator=(DriverInfo const&);
	
public:
	static DriverInfo& getInstance() {
		static DriverInfo instance;
		return instance;
	}
	
	void AddEntryPoint(string type, string funcname, string entrypoint) {
		entry_points[type][funcname] = entrypoint;
	}
	
	bool existsEntryPointWithName(string name) {
		bool exists = false;

		for(map<string, map<string, string> >::iterator i = entry_points.begin(); i != entry_points.end(); i++) {
			for(map<string, string>::iterator j = i->second.begin(); j != i->second.end(); j++) {
				if (name == j->second) {
					exists = true;
					break;
				}
			}
			
			if (exists) break;
		}
		
		return exists;
	}
	
	void PrintDriverInfo() {
		string file = FileName;
		file.append(".info");
		string error_msg;
		llvm::raw_fd_ostream *FOS = new llvm::raw_fd_ostream(file.c_str(), error_msg, llvm::sys::fs::F_None);
		if (!error_msg.empty()) {
	    if (llvm::errs().has_colors()) llvm::errs().changeColor(llvm::raw_ostream::RED);
	    llvm::errs() << "error: " << error_msg << "\n";
	    if (llvm::errs().has_colors()) llvm::errs().resetColor();
	    exit(1);
	  }
		
		FOS->SetUnbuffered();
		FOS->SetUseAtomicWrites(true);
		
		string output = "";
		
		for(map<string, map<string, string> >::iterator i = entry_points.begin(); i != entry_points.end(); i++) {
			output.append("<" + i->first + ">\n");
						
			for(map<string, string>::iterator j = i->second.begin(); j != i->second.end(); j++) {
				output.append(j->first + "::" +j->second + "\n");
			}
			
			output.append("</>\n");
		}
		
		raw_ostream *ros = FOS;
		
		ros->write(output.data(), output.size());
	}
};

class RewriteVisitor : public RecursiveASTVisitor<RewriteVisitor> {
private:
	ASTContext *Context;
	Rewriter RW;
	DriverInfo *DI;
	
public:
  explicit RewriteVisitor(CompilerInstance *CI)
	  : Context(&(CI->getASTContext()))
	{
		RW.setSourceMgr(Context->getSourceManager(), Context->getLangOpts());
	}
	
	virtual bool VisitFunctionDecl(FunctionDecl* FD) {
		string fdFileWithExt = Context->getSourceManager().getFilename(FD->getLocation());
		string fdFile = fdFileWithExt.substr(0, fdFileWithExt.find_last_of("."));
		
		if (DI->getInstance().existsEntryPointWithName(FD->getNameInfo().getName().getAsString())) {			
			if (FD->getStorageClass() == SC_Static) {
				RW.RemoveText(FD->getInnerLocStart(), 7);
			}
		} else if ((fdFile.size() > 0) && (FileName.find(fdFile) != string::npos)) {
			if (FD->getStorageClass() == SC_Static)
				RW.ReplaceText(FD->getInnerLocStart(), 6, "static inline");
		}
		
		return true;
	}
	
	void Finalise() {
		string file = FileName;
		file.append(".re.c");
		
		string error_msg;
		llvm::raw_fd_ostream *FOS = new llvm::raw_fd_ostream(file.c_str(), error_msg, llvm::sys::fs::F_None);
		if (!error_msg.empty()) {
	    if (llvm::errs().has_colors()) llvm::errs().changeColor(llvm::raw_ostream::RED);
	    llvm::errs() << "error: " << error_msg << "\n";
	    if (llvm::errs().has_colors()) llvm::errs().resetColor();
	    exit(1);
	  }
		
		FOS->SetUnbuffered();
		FOS->SetUseAtomicWrites(true);
		
		raw_ostream *ros = FOS;
		
		RW.getEditBuffer(RW.getSourceMgr().getMainFileID()).write(*ros);
	}
};

class FindEntryPointsVisitor : public RecursiveASTVisitor<FindEntryPointsVisitor> {
private:
	ASTContext *Context;
	DriverInfo *DI;

public:
  explicit FindEntryPointsVisitor(CompilerInstance *CI)
		: Context(&(CI->getASTContext()))
	{}
	
	virtual bool VisitVarDecl(VarDecl* VD) {
		if (!VD->getType()->isRecordType()) return true;
	
		RecordDecl *BaseRD = VD->getType()->getAs<RecordType>()->getDecl();
	
		if (!(BaseRD->getNameAsString() == "pci_driver" ||
			  BaseRD->getNameAsString() == "dev_pm_ops" ||
				BaseRD->getNameAsString() == "net_device_ops" ||
			  BaseRD->getNameAsString() == "ethtool_ops" ||
				BaseRD->getNameAsString() == "test_driver")) {
			return true;
		}
		
		InitListExpr *ILE = cast<InitListExpr>(VD->getInit())->getSyntacticForm();
	
		for (Stmt::child_range range = ILE->children(); range; ++range) {				
			DesignatedInitExpr *DIE = cast<DesignatedInitExpr>(*range);				
			if (DIE->size() != 1) continue;
		
			string funcname;
		
			if (/* pci_driver */
				  DIE->getDesignator(0)->getFieldName()->getName() == "probe" ||
				  DIE->getDesignator(0)->getFieldName()->getName() == "remove" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "shutdown" ||
					/* dev_pm_ops */
					DIE->getDesignator(0)->getFieldName()->getName() == "suspend" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "resume" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "freeze" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "thaw" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "poweroff" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "restore" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "runtime_suspend" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "runtime_resume" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "runtime_idle" ||
					/* net_device_ops */
					DIE->getDesignator(0)->getFieldName()->getName() == "ndo_open" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ndo_stop" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ndo_get_stats64" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ndo_start_xmit" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ndo_tx_timeout" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ndo_validate_addr" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ndo_change_mtu" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ndo_fix_features" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ndo_set_features" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ndo_set_mac_address" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ndo_do_ioctl" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ndo_set_rx_mode" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ndo_poll_controller" ||
					/* ethtool_ops */
					DIE->getDesignator(0)->getFieldName()->getName() == "get_drvinfo" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "get_regs_len" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "get_link" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "get_settings" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "set_settings" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "get_msglevel" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "set_msglevel" ||
				  DIE->getDesignator(0)->getFieldName()->getName() == "get_regs" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "get_wol" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "set_wol" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "get_strings" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "get_sset_count" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "get_ethtool_stats" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "get_ts_info" ||
					/* test_driver */
					DIE->getDesignator(0)->getFieldName()->getName() == "ep1" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ep2" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ep3" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ep4" ||
					DIE->getDesignator(0)->getFieldName()->getName() == "ep5")
				funcname = DIE->getDesignator(0)->getFieldName()->getName();
			else
				continue;
		
			Expr *expr = cast<ImplicitCastExpr>(DIE->getInit())->getSubExpr();
			while (!isa<DeclRefExpr>(expr))
				expr = cast<ImplicitCastExpr>(expr)->getSubExpr();
			DeclRefExpr *DRE = cast<DeclRefExpr>(expr);
					
			string fdFileWithExt = Context->getSourceManager().getFilename(DRE->getDecl()->getLocation());
			string fdFile = fdFileWithExt.substr(0, fdFileWithExt.find_last_of("."));
			
		  if ((fdFile.size() > 0) && (fdFile.find(FileName) != string::npos)) {
				DI->getInstance().AddEntryPoint(BaseRD->getNameAsString(), funcname, DRE->getNameInfo().getName().getAsString());
			}
		}
	
		return true;
	}
	
	void PrintEntryPoints() {
		DI->getInstance().PrintDriverInfo();
	}	
};

class ParseDriverConsumer : public ASTConsumer {
private:
  FindEntryPointsVisitor *FEPV;
	RewriteVisitor *RV;

public:
  explicit ParseDriverConsumer(CompilerInstance *CI)
    : FEPV(new FindEntryPointsVisitor(CI)), RV(new RewriteVisitor(CI))
	{}
	
  virtual void HandleTranslationUnit(ASTContext &Context) {
    FEPV->TraverseDecl(Context.getTranslationUnitDecl());
		FEPV->PrintEntryPoints();
		RV->TraverseDecl(Context.getTranslationUnitDecl());
		RV->Finalise();
	}
};

class ParseDriverASTAction : public ASTFrontendAction {
public:
    virtual ASTConsumer *CreateASTConsumer(CompilerInstance &CI, StringRef file) {
        return new ParseDriverConsumer(&CI);
    }
};

int main(int argc, const char **argv) {
	CommonOptionsParser op(argc, argv);
	ClangTool Tool(op.getCompilations(), op.getSourcePathList());
	FileName = op.getSourcePathList()[0].substr(0, op.getSourcePathList()[0].find_last_of("."));
	return Tool.run(newFrontendActionFactory<ParseDriverASTAction>());
}
